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
        private HttpModuleOptions _options = new();
        private Dictionary<string, string> _defaultHeaders;

        private readonly Queue<HttpQueueEntity> _queue = new();
        private bool _isProcessing;

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

        public void Configure(HttpModuleOptions options) => _options = options;

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
                        _options.BlockingHandler?.Invoke(entity, true);

                    try
                    {
                        await SendWithRetry(entity);

                        if (CheckKick(entity)) return;

                        if (_options.ReLoginCode != 0 && entity.Code == _options.ReLoginCode && _options.ReLoginHandler != null)
                        {
                            var success = await _options.ReLoginHandler(GFCancellationToken);
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
                            _options.ErrorHandler?.Invoke(entity.Code, entity.Msg);
                        }
                    }
                    finally
                    {
                        if (entity.IsBlocking)
                            _options.BlockingHandler?.Invoke(entity, false);

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

            HttpResponse raw;
            try
            {
                raw = await _provider.SendAsync(url, method, bodyBytes, _defaultHeaders, _options.TimeoutSeconds, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                LogError($"[HTTP] 请求异常 {entity.Path}: {ex.Message}");
                entity.Code = HttpEntityBase.CodeNetworkError;
                entity.Msg = ex.Message;
                return;
            }

            if (!raw.IsSuccess)
            {
                entity.Code = raw.IsTimeout ? HttpEntityBase.CodeNetworkError : HttpEntityBase.CodeHttpError;
                entity.Msg = raw.Error ?? $"HTTP {raw.StatusCode}";
                if (raw.IsTimeout)
                    LogWarning($"[HTTP] 请求超时 {entity.Path} ({raw.ElapsedMs}ms)");
                else
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
