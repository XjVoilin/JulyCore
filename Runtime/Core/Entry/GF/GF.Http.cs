using System.Threading;
using Cysharp.Threading.Tasks;
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

            public static void Configure(string baseUrl, int timeoutSeconds = 15)
            {
                Module.Configure(baseUrl, timeoutSeconds);
            }

            public static UniTask Send(HttpEntityBase entity, CancellationToken ct = default)
            {
                return Module.Send(entity, ct);
            }
        }
    }
}
