using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Data.Network;
using JulyCore.Module.Base;
using JulyCore.Provider.Http;

namespace JulyCore.Module.Http
{
    public class HttpModule : ModuleBase
    {
        private IHttpProvider _provider;

        private string _baseUrl;
        private int _timeoutSeconds = 10;
        private Dictionary<string, string> _defaultHeaders;

        private int _reLoginCode;
        private Func<CancellationToken, UniTask<bool>> _reLoginHandler;
        private Action<int, string> _errorHandler;
        private int _kickCode;
        private Action _kickHandler;
        private Action<HttpQueueEntity, bool> _blockingHandler;

        private readonly Queue<HttpQueueEntity> _queue = new();
        private bool _isProcessing;

        private int _retryBaseDelayMs = 1000;
        private float _retryBackoffMultiplier = 2f;
        private int _retryMaxDelayMs = 10000;

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
        }

        private void ClearQueue()
        {
            while (_queue.Count > 0)
                _queue.Dequeue().SetCompleted();
        }

        #region Configure

        public void Configure(HttpModuleOptions options)
        {
            _baseUrl = options.BaseUrl;
            _timeoutSeconds = options.TimeoutSeconds;
            _errorHandler = options.ErrorHandler;
            _reLoginCode = options.ReLoginCode;
            _reLoginHandler = options.ReLoginHandler;
            _kickCode = options.KickCode;
            _kickHandler = options.KickHandler;
            _blockingHandler = options.BlockingHandler;
            _retryBaseDelayMs = options.RetryBaseDelayMs;
            _retryBackoffMultiplier = options.RetryBackoffMultiplier;
            _retryMaxDelayMs = options.RetryMaxDelayMs;
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
            await SendInternal(entity, ct);

            if (CheckKick(entity)) return;

            if (!entity.IsOk && _reLoginCode != 0 && entity.Code == _reLoginCode && _reLoginHandler != null)
            {
                if (await _reLoginHandler(ct))
                {
                    await SendInternal(entity, ct);
                    if (CheckKick(entity)) return;
                }
            }

            if (!entity.IsOk)
                _errorHandler?.Invoke(entity.Code, entity.Msg);
        }

        #endregion

        #region Send — Queue Path

        public void Send(HttpQueueEntity entity)
        {
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

                    if (entity.IsBlocking)
                        _blockingHandler?.Invoke(entity, true);

                    try
                    {
                        await SendWithRetry(entity);

                        if (CheckKick(entity)) return;

                        if (_reLoginCode != 0 && entity.Code == _reLoginCode && _reLoginHandler != null)
                        {
                            var success = await _reLoginHandler(GFCancellationToken);
                            if (!success) return;

                            entity.RegenerateRequestId();
                            await SendWithRetry(entity);

                            if (CheckKick(entity)) return;
                        }

                        if (entity.IsOk)
                        {
                            try { entity.OnResponse(); }
                            catch (Exception ex) { LogError($"[HTTP] OnResponse 异常: {ex.Message}"); }
                        }
                        else
                        {
                            try { entity.OnError(); }
                            catch (Exception ex) { LogError($"[HTTP] OnError 异常: {ex.Message}"); }
                            _errorHandler?.Invoke(entity.Code, entity.Msg);
                        }
                    }
                    finally
                    {
                        if (entity.IsBlocking)
                            _blockingHandler?.Invoke(entity, false);

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

                if (entity.IsOk || entity.Code > 0)
                    break;

                var delay = (int)Math.Min(
                    _retryBaseDelayMs * Math.Pow(_retryBackoffMultiplier, retryCount),
                    _retryMaxDelayMs);

                if (IsLogEnabled)
                    Log($"[HTTP] 重试 #{retryCount + 1}，{delay}ms 后重发 {entity.Path}");

                await UniTask.Delay(delay, cancellationToken: GFCancellationToken);
                retryCount++;
            }
        }

        #endregion

        #region Internal

        private bool CheckKick(HttpEntityBase entity)
        {
            if (_kickCode == 0 || entity.IsOk || entity.Code != _kickCode) return false;
            _kickHandler?.Invoke();
            return true;
        }

        private async UniTask SendInternal(HttpEntityBase entity, CancellationToken ct)
        {
            var logName = entity.LogTag != null ? $"{entity.Path} [{entity.LogTag}]" : entity.Path;
            var bodyJson = entity.BuildBody();

            if (IsLogEnabled)
                Log($"[HTTP] >>> {logName}\n{bodyJson ?? "(empty)"}");

            var raw = await SendRawAsync(entity.Path, bodyJson, ct);

            if (raw.IsSuccess)
            {
                try
                {
                    entity.ParseResponse(raw.Text);
                    if (IsLogEnabled)
                        Log($"[HTTP] <<< {logName} code={entity.Code}\n{raw.Text}");
                }
                catch (Exception ex)
                {
                    LogError($"[HTTP] <<< {logName} 响应解析失败: {ex.Message}");
                    entity.Code = -1;
                    entity.Msg = $"响应解析失败: {ex.Message}";
                }
            }
            else
            {
                LogWarning($"[HTTP] <<< {logName} 失败: {raw.Error}");
                entity.Code = -1;
                entity.Msg = raw.Error;
            }
        }

        private struct RawResult
        {
            public bool IsSuccess;
            public string Text;
            public string Error;
        }

        private async UniTask<RawResult> SendRawAsync(string path, string bodyJson, CancellationToken ct)
        {
            if (_provider == null)
                throw new InvalidOperationException("IHttpProvider 未注册");

            var url = BuildUrl(path);
            byte[] bodyBytes = null;
            var method = "GET";

            if (bodyJson != null)
            {
                bodyBytes = Encoding.UTF8.GetBytes(bodyJson);
                method = "POST";
            }

            HttpResponse raw;
            try
            {
                raw = await _provider.SendAsync(url, method, bodyBytes, _defaultHeaders, _timeoutSeconds, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                LogError($"[HTTP] 请求异常 {path}: {ex.Message}");
                return new RawResult { Error = ex.Message };
            }

            if (!raw.IsSuccess)
            {
                LogWarning($"[HTTP] 请求失败 {path}: {raw.StatusCode} {raw.Error}");
                return new RawResult { Error = raw.Error ?? $"HTTP {raw.StatusCode}" };
            }

            return new RawResult { IsSuccess = true, Text = raw.GetText() };
        }

        private string BuildUrl(string path)
        {
            if (string.IsNullOrEmpty(_baseUrl) || path.StartsWith("http"))
                return path;
            return _baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
        }

        #endregion
    }
}
