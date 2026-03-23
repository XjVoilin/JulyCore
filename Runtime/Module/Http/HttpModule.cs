using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Core.Events;
using JulyCore.Data.Network;
using JulyCore.Module.Base;
using JulyCore.Provider.Http;

namespace JulyCore.Module.Http
{
    public class HttpModule : ModuleBase
    {
        private IHttpProvider _httpProvider;

        protected override LogChannel LogChannel => LogChannel.Network;

        public override int Priority => Frameworkconst.PriorityHttpModule;

        protected override UniTask OnInitAsync()
        {
            _httpProvider = GetProvider<IHttpProvider>();
            if (_httpProvider == null)
                LogWarning($"[{Name}] IHttpProvider 未注册，HTTP 功能不可用");
            return UniTask.CompletedTask;
        }

        protected override UniTask OnShutdownAsync()
        {
            _httpProvider = null;
            return UniTask.CompletedTask;
        }

        public void ConfigureHttp(HttpConfig config)
        {
            EnsureProvider();
            _httpProvider.ConfigureHttp(config);
        }

        public async UniTask<HttpResponse> GetAsync(string url,
            Dictionary<string, string> headers = null,
            CancellationToken cancellationToken = default)
        {
            EnsureProvider();
            var response = await _httpProvider.GetAsync(url, headers, cancellationToken);
            PublishHttpEvent("GET", url, response);
            return response;
        }

        public async UniTask<HttpResponse> PostAsync(string url, byte[] data,
            Dictionary<string, string> headers = null,
            CancellationToken cancellationToken = default)
        {
            EnsureProvider();
            var response = await _httpProvider.PostAsync(url, data, headers, cancellationToken);
            PublishHttpEvent("POST", url, response);
            return response;
        }

        public async UniTask<HttpResponse> PostJsonAsync(string url, string jsonData,
            Dictionary<string, string> headers = null,
            CancellationToken cancellationToken = default)
        {
            EnsureProvider();
            var response = await _httpProvider.PostJsonAsync(url, jsonData, headers,
                cancellationToken);
            PublishHttpEvent("POST", url, response);
            return response;
        }

        public async UniTask<HttpResponse> PutAsync(string url, byte[] data,
            Dictionary<string, string> headers = null,
            CancellationToken cancellationToken = default)
        {
            EnsureProvider();
            var response = await _httpProvider.PutAsync(url, data, headers, cancellationToken);
            PublishHttpEvent("PUT", url, response);
            return response;
        }

        public async UniTask<HttpResponse> DeleteAsync(string url,
            Dictionary<string, string> headers = null,
            CancellationToken cancellationToken = default)
        {
            EnsureProvider();
            var response = await _httpProvider.DeleteAsync(url, headers, cancellationToken);
            PublishHttpEvent("DELETE", url, response);
            return response;
        }

        private void EnsureProvider()
        {
            if (_httpProvider == null)
                throw new System.InvalidOperationException(
                    "IHttpProvider 未注册，请在 OnConfigureBase 中注册");
        }

        private void PublishHttpEvent(string method, string url, HttpResponse response)
        {
            EventBus?.Publish(new HttpRequestCompletedEvent
            {
                Method = method,
                Url = url,
                Response = response,
                IsSuccess = response.IsSuccess
            });
        }
    }
}
