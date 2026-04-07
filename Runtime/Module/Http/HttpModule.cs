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
        private int _timeoutSeconds = 15;
        private Dictionary<string, string> _defaultHeaders;

        private int _reLoginCode;
        private Func<CancellationToken, UniTask<bool>> _reLoginHandler;

        public void SetReLoginHandler(int errorCode, Func<CancellationToken, UniTask<bool>> handler)
        {
            _reLoginCode = errorCode;
            _reLoginHandler = handler;
        }

        protected override LogChannel LogChannel => LogChannel.Network;
        public override int Priority => Frameworkconst.PriorityHttpModule;

        protected override UniTask OnInitAsync()
        {
            _provider = GetProvider<IHttpProvider>();
            if (_provider == null)
                LogWarning($"[{Name}] IHttpProvider 未注册，HTTP 功能不可用");

            return UniTask.CompletedTask;
        }

        protected override UniTask OnShutdownAsync()
        {
            _provider = null;
            _defaultHeaders = null;
            return UniTask.CompletedTask;
        }

        public void Configure(string baseUrl, int timeoutSeconds = 15)
        {
            _baseUrl = baseUrl;
            _timeoutSeconds = timeoutSeconds;
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

        public async UniTask Send(HttpEntityBase entity, CancellationToken ct = default)
        {
            await SendInternal(entity, ct);

            if (!entity.IsOk && entity.Code == _reLoginCode && _reLoginHandler != null)
            {
                if (await _reLoginHandler(ct))
                    await SendInternal(entity, ct);
            }
        }

        private async UniTask SendInternal(HttpEntityBase entity, CancellationToken ct)
        {
            var bodyJson = entity.BuildBody();
            var raw = await SendRawAsync(entity.Path, bodyJson, ct);

            if (raw.IsSuccess)
            {
                try
                {
                    entity.ParseResponse(raw.Text);
                }
                catch (Exception ex)
                {
                    LogError($"[HTTP] 响应解析失败 {entity.Path}: {ex.Message}");
                    entity.Code = -1;
                    entity.Msg = $"响应解析失败: {ex.Message}";
                }
            }
            else
            {
                entity.Code = -1;
                entity.Msg = raw.Error;
            }

        }

        #region Internal

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
            catch (OperationCanceledException)
            {
                return new RawResult { Error = "请求已取消" };
            }
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
