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

        public Action<bool> BlockingHandler;

        public int DirectMaxRetryCount = 3;
        public int RetryBaseDelayMs = 1000;
        public float RetryBackoffMultiplier = 2f;
        public int RetryMaxDelayMs = 10000;

        /// <summary>
        /// 设置后启用队列持久化，值为 ISaveProvider 的存档键。
        /// 为 null/empty 时不启用。
        /// </summary>
        public string PendingQueueSaveKey;
    }
}
