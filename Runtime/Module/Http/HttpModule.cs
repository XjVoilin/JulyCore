using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Data.Network;
using JulyCore.Module.Base;
using JulyCore.Provider.Http;
using JulyCore.Provider.Save;

namespace JulyCore.Module.Http
{
    public class HttpModule : ModuleBase
    {
        private IHttpProvider _provider;
        private HttpModuleOptions _options = new();
        private Dictionary<string, string> _defaultHeaders;

        private readonly Queue<HttpQueueEntity> _queue = new();
        private bool _isProcessing;
        private bool _blockingShown;

        private ISaveProvider _saveProvider;
        private HttpPendingQueueData _pendingData;
        private string _pendingQueueSaveKey;

        protected override LogChannel LogChannel => LogChannel.Network;
        public override int Priority => Frameworkconst.PriorityHttpModule;

        protected override UniTask OnInitAsync()
        {
            _provider = GetProvider<IHttpProvider>();
            return UniTask.CompletedTask;
        }

        protected override void OnShutdown()
        {
            _provider = null;
            _defaultHeaders = null;
            ClearQueue();
            _isProcessing = false;
            SetBlocking(false);
        }

        private void ClearQueue()
        {
            while (_queue.Count > 0)
                _queue.Dequeue().SetCompleted();
        }

        private void SetBlocking(bool show)
        {
            if (show == _blockingShown) return;
            _blockingShown = show;
            _options.BlockingHandler?.Invoke(show);
        }

        #region Configure

        public async UniTask Configure(HttpModuleOptions options)
        {
            _options = options;

            if (!string.IsNullOrEmpty(options.PendingQueueSaveKey))
            {
                _pendingQueueSaveKey = options.PendingQueueSaveKey;
                _saveProvider = GetProvider<ISaveProvider>();
                _pendingData = await _saveProvider.LoadAndRegisterAsync<HttpPendingQueueData>(
                    _pendingQueueSaveKey, GFCancellationToken);
                Log($"[HTTP] 持久化队列已加载，待补发 {_pendingData.Entries.Count} 条");
            }
        }

        public void SetDefaultHeader(string key, string value)
        {
            _defaultHeaders ??= new Dictionary<string, string>();
            _defaultHeaders[key] = value;
        }

        public void RemoveDefaultHeader(string key)
        {
            _defaultHeaders?.Remove(key);
        }

        #endregion

        #region Send — Direct Path

        /// <summary>
        /// OperationCanceledException 会原样抛给调用方，不触发 errorHandler。
        /// </summary>
        public async UniTask Send(HttpEntity entity, CancellationToken ct = default)
        {
            var maxRetry = entity.MaxRetryCount >= 0 ? entity.MaxRetryCount : _options.DirectMaxRetryCount;
            var retryCount = 0;

            while (true)
            {
                await SendInternal(entity, ct);

                if (entity.Code != HttpEntityBase.CodeNetworkError)
                    break;

                if (retryCount >= maxRetry)
                    break;

                var delay = (int)Math.Min(
                    _options.RetryBaseDelayMs * Math.Pow(_options.RetryBackoffMultiplier, retryCount),
                    _options.RetryMaxDelayMs);

                Log($"[HTTP] 直发重试 #{retryCount + 1}，{delay}ms 后重发 {entity.Path}");

                await UniTask.Delay(delay, cancellationToken: ct);
                retryCount++;
            }

            if (CheckKick(entity)) return;

            if (!entity.IsOk && _options.ReLoginCode != 0 && entity.Code == _options.ReLoginCode && _options.ReLoginHandler != null)
            {
                if (await _options.ReLoginHandler(ct))
                {
                    await SendInternal(entity, ct);
                    if (CheckKick(entity)) return;
                }
            }

            if (!entity.IsOk)
                _options.ErrorHandler?.Invoke(entity.Code, entity.Msg);
        }

        #endregion

        #region Send — Queue Path

        public void Send(HttpQueueEntity entity)
        {
            if (entity.IsOptimistic)
                entity.ApplyLocal();

            if (_pendingData != null)
                PersistPendingEntry(entity);

            _queue.Enqueue(entity);
            if (!_isProcessing)
                ProcessQueueAsync().Forget();
        }

        private async UniTask ProcessQueueAsync()
        {
            _isProcessing = true;
            try
            {
                while (_queue.Count > 0)
                {
                    var entity = _queue.Dequeue();

                    if (!entity.IsOptimistic)
                        SetBlocking(true);

                    var removePending = true;
                    try
                    {
                        await SendWithRetry(entity);

                        if (CheckKick(entity)) { removePending = false; return; }

                        if (_options.ReLoginCode != 0 && entity.Code == _options.ReLoginCode && _options.ReLoginHandler != null)
                        {
                            var success = await _options.ReLoginHandler(GFCancellationToken);
                            if (!success) { removePending = false; return; }

                            entity.RegenerateRequestId();
                            await SendWithRetry(entity);

                            if (CheckKick(entity)) { removePending = false; return; }
                        }

                        if (entity.IsOk)
                        {
                            try { entity.OnResponse(); }
                            catch (Exception ex) { LogError($"[HTTP] OnResponse 异常: {ex.Message}"); }
                        }
                        else if (entity.Code < 0)
                        {
                            _options.ErrorHandler?.Invoke(entity.Code, entity.Msg);
                        }
                        else
                        {
                            try { entity.OnError(); }
                            catch (Exception ex) { LogError($"[HTTP] OnError 异常: {ex.Message}"); }
                            _options.ErrorHandler?.Invoke(entity.Code, entity.Msg);
                        }
                    }
                    finally
                    {
                        if (_pendingData != null && removePending)
                            await RemovePendingEntryAsync();

                        if (!entity.IsOptimistic)
                            SetBlocking(false);

                        entity.SetCompleted();
                    }
                }
            }
            finally
            {
                ClearQueue();
                _isProcessing = false;
            }
        }

        private async UniTask SendWithRetry(HttpQueueEntity entity)
        {
            var retryCount = 0;

            while (true)
            {
                await SendInternal(entity, GFCancellationToken);

                if (entity.Code != HttpEntityBase.CodeNetworkError)
                    break;

                var delay = (int)Math.Min(
                    _options.RetryBaseDelayMs * Math.Pow(_options.RetryBackoffMultiplier, retryCount),
                    _options.RetryMaxDelayMs);

                Log($"[HTTP] 重试 #{retryCount + 1}，{delay}ms 后重发 {entity.Path}");

                await UniTask.Delay(delay, cancellationToken: GFCancellationToken);
                retryCount++;
            }

        }

        #endregion

        public bool HasPendingEntries()
            => _pendingData != null && _pendingData.Entries.Count > 0;

        #region Replay — 补发流程

        private class ReplayEntry : HttpEntityBase
        {
            public override string Path { get; }
            private readonly string _body;

            internal ReplayEntry(string path, string body)
            {
                Path = path;
                _body = body;
            }

            protected internal override string BuildBody() => _body;
            protected override void SetResponseData(string dataJson) { }
        }

        public UniTask ReplayPending()
        {
            if (_pendingData == null || _pendingData.Entries.Count == 0) 
                return UniTask.CompletedTask;
            Log($"[HTTP] 开始补发 {_pendingData.Entries.Count} 条持久化消息");
            return ReplayPendingAsync();
        }

        private async UniTask ReplayPendingAsync()
        {
            while (_pendingData.Entries.Count > 0)
            {
                var entry = _pendingData.Entries[0];
                var entity = new ReplayEntry(entry.Path, entry.Body);

                var retryCount = 0;
                while (true)
                {
                    await SendInternal(entity, GFCancellationToken);

                    if (entity.Code != HttpEntityBase.CodeNetworkError)
                        break;

                    var delay = (int)Math.Min(
                        _options.RetryBaseDelayMs * Math.Pow(_options.RetryBackoffMultiplier, retryCount),
                        _options.RetryMaxDelayMs);

                    Log($"[HTTP] 补发重试 #{retryCount + 1}，{delay}ms 后重发 {entry.Path}");

                    await UniTask.Delay(delay, cancellationToken: GFCancellationToken);
                    retryCount++;
                }

                _pendingData.Entries.RemoveAt(0);
                await _saveProvider.SaveAsync(_pendingQueueSaveKey, _pendingData);
                Log($"[HTTP] 补发完成: {entry.Path}，剩余 {_pendingData.Entries.Count} 条");
            }
        }

        #endregion

        #region Pending — 持久化操作

        private void PersistPendingEntry(HttpQueueEntity entity)
        {
            _pendingData.Entries.Add(new HttpPendingEntry
            {
                Path = entity.Path,
                Body = entity.BuildBody()
            });
            _saveProvider.SaveAsync(_pendingQueueSaveKey, _pendingData).Forget();
            Log($"[HTTP] 持久化队列消息: {entity.Path}，当前 {_pendingData.Entries.Count} 条");
        }

        private async UniTask RemovePendingEntryAsync()
        {
            if (_pendingData.Entries.Count == 0) return;
            var removed = _pendingData.Entries[0];
            _pendingData.Entries.RemoveAt(0);
            await _saveProvider.SaveAsync(_pendingQueueSaveKey, _pendingData);
            Log($"[HTTP] 移除持久化条目: {removed.Path}，剩余 {_pendingData.Entries.Count} 条");
        }

        #endregion

        #region Internal

        private bool CheckKick(HttpEntityBase entity)
        {
            if (_options.KickCode == 0 || entity.IsOk || entity.Code != _options.KickCode) return false;
            _options.KickHandler?.Invoke();
            return true;
        }

        private async UniTask SendInternal(HttpEntityBase entity, CancellationToken ct)
        {
            if (_provider == null)
                throw new InvalidOperationException("IHttpProvider 未注册");

            var logName = entity.LogTag != null ? $"{entity.Path} [{entity.LogTag}]" : entity.Path;
            var bodyJson = entity.BuildBody();
            Log($"[HTTP] >>> {logName}\n{bodyJson ?? "(empty)"}");

            var url = BuildUrl(entity.Path);
            byte[] bodyBytes = null;
            var method = "GET";

            if (bodyJson != null)
            {
                bodyBytes = Encoding.UTF8.GetBytes(bodyJson);
                method = "POST";
            }

            var raw = await _provider.SendAsync(url, method, bodyBytes, _defaultHeaders, _options.TimeoutSeconds, ct);

            if (raw.IsNetworkError)
            {
                entity.Code = HttpEntityBase.CodeNetworkError;
                entity.Msg = raw.Error ?? "Network error";
                LogWarning($"[HTTP] 网络错误 {entity.Path}: {raw.Error} ({raw.ElapsedMs}ms)");
                return;
            }

            if (!raw.IsSuccess)
            {
                entity.Code = HttpEntityBase.CodeHttpError;
                entity.Msg = raw.Error ?? $"HTTP {raw.StatusCode}";
                LogWarning($"[HTTP] 请求失败 {entity.Path}: {raw.StatusCode} {raw.Error}");
                return;
            }

            var text = raw.GetText();
            try
            {
                entity.ParseResponse(text);
                Log($"[HTTP] <<< {logName} code={entity.Code}\n{text}");
            }
            catch (Exception ex)
            {
                LogError($"[HTTP] <<< {logName} 响应解析失败: {ex.Message}");
                entity.Code = HttpEntityBase.CodeParseError;
                entity.Msg = $"响应解析失败: {ex.Message}";
            }
        }

        private string BuildUrl(string path)
        {
            if (string.IsNullOrEmpty(_options.BaseUrl) || path.StartsWith("http"))
                return path;
            return _options.BaseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
        }

        #endregion
    }
}
