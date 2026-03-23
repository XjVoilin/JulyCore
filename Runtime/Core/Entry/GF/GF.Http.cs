using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core.Events;
using JulyCore.Data.Network;
using JulyCore.Module.Http;

namespace JulyCore
{
    public static partial class GF
    {
        public static class Http
        {
            private static HttpModule _module;
            private static HttpModule Module
            {
                get
                {
                    _module ??= GetModule<HttpModule>();
                    return _module;
                }
            }

            public static void Configure(HttpConfig config)
            {
                Module.ConfigureHttp(config);
            }

            public static UniTask<HttpResponse> GetAsync(string url,
                Dictionary<string, string> headers = null,
                CancellationToken cancellationToken = default)
            {
                return Module.GetAsync(url, headers, cancellationToken);
            }

            public static UniTask<HttpResponse> PostAsync(string url, byte[] data,
                Dictionary<string, string> headers = null,
                CancellationToken cancellationToken = default)
            {
                return Module.PostAsync(url, data, headers, cancellationToken);
            }

            public static UniTask<HttpResponse> PostJsonAsync(string url, string jsonData,
                Dictionary<string, string> headers = null,
                CancellationToken cancellationToken = default)
            {
                return Module.PostJsonAsync(url, jsonData, headers, cancellationToken);
            }

            public static UniTask<HttpResponse> PutAsync(string url, byte[] data,
                Dictionary<string, string> headers = null,
                CancellationToken cancellationToken = default)
            {
                return Module.PutAsync(url, data, headers, cancellationToken);
            }

            public static UniTask<HttpResponse> DeleteAsync(string url,
                Dictionary<string, string> headers = null,
                CancellationToken cancellationToken = default)
            {
                return Module.DeleteAsync(url, headers, cancellationToken);
            }

            public static void OnHttpCompleted(
                System.Action<HttpRequestCompletedEvent> handler, object target)
            {
                _context.EventBus?.Subscribe(handler, target);
            }
        }
    }
}
