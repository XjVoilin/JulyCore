using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace JulyCore.Module.Http
{
    public class HttpModuleOptions
    {
        public string BaseUrl;
        public int TimeoutSeconds = 10;

        public Action<int, string> ErrorHandler;

        public int ReLoginCode;
        public Func<CancellationToken, UniTask<bool>> ReLoginHandler;

        public int KickCode;
        public Action KickHandler;

        public Action<HttpQueueEntity, bool> BlockingHandler;

        public int RetryBaseDelayMs = 1000;
        public float RetryBackoffMultiplier = 2f;
        public int RetryMaxDelayMs = 10000;
    }
}
