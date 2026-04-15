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

            public static UniTask Configure(HttpModuleOptions options, IHttpHandler handler)
            {
                return Module.Configure(options, handler);
            }

            public static UniTask Send(HttpEntity entity, CancellationToken ct = default)
            {
                return Module.Send(entity, ct);
            }

            public static void Send(HttpQueueEntity entity)
            {
                Module.Send(entity);
            }

            public static void SetDefaultHeader(string key, string value)
            {
                Module.SetDefaultHeader(key, value);
            }

            public static bool HasPendingEntries()
            {
                return Module.HasPendingEntries();
            }

            public static UniTask ReplayPending()
            {
                return Module.ReplayPending();
            }

            public static void RemoveDefaultHeader(string key)
            {
                Module.RemoveDefaultHeader(key);
            }
        }
    }
}
