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
        private IHttpHandler _handler;
        private HttpModuleOptions _options = new();
        private Dictionary<string, string> _defaultHeaders;

        private readonly Queue<HttpQueueEntity> _queue = new();
        private bool _isProcessing;
        /// <summary>
        /// 队列是否是悲观模式
        /// </summary>
        private bool _isPessimistic;
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
            ClearQueue();
            _isProcessing = false;
            _isPessimistic = false;
            SetBlocking(false);
            _provider = null;
            _handler = null;
            _defaultHeaders = null;
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
            _handler?.OnBlockingChanged(show);
        }

        #region Configure

        public async UniTask Configure(HttpModuleOptions options, IHttpHandler handler)
        {
            _options = options;
            _handler = handler;

            if (!string.IsNullOrEmpty(options.PendingQueueSaveKey))
            {
                _pendingQueueSaveKey = options.PendingQueueSaveKey;
                _saveProvider = GetProvider<ISaveProvider>();
                _pendingData = await _saveProvider.LoadAndRegisterAsync<HttpPendingQueueData>(
                    _pendingQueueSaveKey);
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
        /// OperationCanceledException 会原样抛给调用方，不触发 OnError。
        /// </summary>
        public async UniTask Send(HttpEntity entity, CancellationToken ct = default)
        {
            var maxRetry = entity.MaxRetryCount >= 0 ? entity.MaxRetryCount : _options.MaxRetryCount;
            var retryCount = 0;

            while (true)
            {
                await SendInternal(entity, ct);

                if (entity.Code != HttpEntityBase.CodeNetworkError)
                    break;

                if (retryCount >= maxRetry)
                    break;

                var delay = _options.CalculateRetryDelay(retryCount);
                LogWarning($"[HTTP] 直发重试 #{retryCount + 1}，{delay}ms 后重发 {entity.Path}");
                await UniTask.Delay(delay, cancellationToken: ct);
                retryCount++;
            }

            if (CheckKick(entity)) return;

            if (!entity.IsOk && _options.ReLoginCode != 0
                              && entity.Code == _options.ReLoginCode)
            {
                if (_handler != null && await _handler.OnReLoginRequired(ct))
                {
                    await SendInternal(entity, ct);
                    if (CheckKick(entity)) return;
                }
            }

            if (!entity.IsOk)
                _handler?.OnError(entity.Code, entity.Msg);
        }

        #endregion

        #region Send — Queue Path

        public void Send(HttpQueueEntity entity)
        {
            if (entity.IsOptimistic)
                entity.ApplyLocal();

            if (_pendingData != null)
                PersistPendingEntry(entity);

            if (!entity.IsOptimistic && !_isPessimistic)
            {
                _isPessimistic = true;
                SetBlocking(true);
            }

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
                    var removePending = true;
                    try
                    {
                        await SendWithRetry(entity);

                        if (CheckKick(entity)) { removePending = false; return; }

                        if (!entity.IsOk && _options.ReLoginCode != 0
                            && entity.Code == _options.ReLoginCode)
                        {
                            var success = _handler != null && await _handler.OnReLoginRequired(default);
                            if (!success) { removePending = false; return; }

                            entity.RegenerateRequestId();
                            await SendWithRetry(entity);

                            if (CheckKick(entity)) { removePending = false; return; }
                        }

                        DispatchResult(entity);
                    }
                    catch (OperationCanceledException)
                    {
                        removePending = false;
                        return;
                    }
                    finally
                    {
                        if (_pendingData != null && removePending)
                            await RemovePendingEntryAsync();
                        entity.SetCompleted();
                    }
                }
            }
            finally
            {
                ClearQueue();
                _isProcessing = false;
                _isPessimistic = false;
                SetBlocking(false);
            }
        }

        private void DispatchResult(HttpQueueEntity entity)
        {
            if (entity.IsOk)
            {
                try { entity.OnResponse(); }
                catch (Exception ex) { LogError($"[HTTP] OnResponse 异常: {ex.Message}"); }
                return;
            }

            if (entity.Code < 0)
            {
                _handler?.OnError(entity.Code, entity.Msg);
                return;
            }

            try { entity.OnError(); }
            catch (Exception ex) { LogError($"[HTTP] OnError 异常: {ex.Message}"); }
            _handler?.OnError(entity.Code, entity.Msg);
        }

        private async UniTask SendWithRetry(HttpQueueEntity entity)
        {
            var retryCount = 0;

            while (true)
            {
                await SendInternal(entity, default);

                if (entity.Code != HttpEntityBase.CodeNetworkError)
                    break;

                retryCount++;

                if (_isPessimistic && retryCount >= _options.MaxRetryCount)
                {
                    LogWarning($"[HTTP] 队列重试达上限 {retryCount} 次，等待用户决策 {entity.Path}");
                    var shouldContinue = _handler != null && await _handler.OnRetryExceeded();
                    if (!shouldContinue) break;
                    retryCount = 0;
                    continue;
                }

                var delay = _options.CalculateRetryDelay(retryCount - 1);
                LogWarning($"[HTTP] 队列重试 #{retryCount}，{delay}ms 后重发 {entity.Path}");
                await UniTask.Delay(delay);
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
                    await SendInternal(entity, default);

                    if (entity.Code != HttpEntityBase.CodeNetworkError)
                        break;

                    var delay = _options.CalculateRetryDelay(retryCount);
                    LogWarning($"[HTTP] 补发重试 #{retryCount + 1}，{delay}ms 后重发 {entry.Path}");
                    await UniTask.Delay(delay);
                    retryCount++;
                }

                _pendingData.Entries.RemoveAt(0);
                await _saveProvider.SaveAsync(_pendingQueueSaveKey, _pendingData);
            }

            Log("[HTTP] 补发全部完成");
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
        }

        private async UniTask RemovePendingEntryAsync()
        {
            if (_pendingData.Entries.Count == 0) return;
            _pendingData.Entries.RemoveAt(0);
            await _saveProvider.SaveAsync(_pendingQueueSaveKey, _pendingData);
        }

        #endregion

        #region Internal

        private bool CheckKick(HttpEntityBase entity)
        {
            if (_options.KickCode == 0 || entity.IsOk || entity.Code != _options.KickCode) return false;
            _handler?.OnKicked();
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
