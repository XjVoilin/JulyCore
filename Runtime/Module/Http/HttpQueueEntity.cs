using System;
using Cysharp.Threading.Tasks;

namespace JulyCore.Module.Http
{
    /// <summary>
    /// 队列请求基类。Send(HttpQueueEntity) 入队即返回（void），由队列后台处理。
    /// 与 HttpEntity 平行，编译器强制隔离两条发送路径。
    /// </summary>
    public abstract class HttpQueueEntity : HttpEntityBase
    {
        public uint RequestId { get; private set; } = GenerateRequestId();

        public void RegenerateRequestId() => RequestId = GenerateRequestId();

        private static uint GenerateRequestId()
            => (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        /// <summary>
        /// 是否乐观
        /// 乐观说明:
        ///     1: 调用即本地相应,框架自动调用ApplyLocal,Entity中ApplyLocal和OnResponse逻辑要区分
        ///     2: 不需要等待遮罩
        /// </summary>
        public virtual bool IsOptimistic => true;

        /// <summary>
        /// 乐观预更新。IsOptimistic=true 时由 HttpModule.Send 在入队前自动调用。
        /// IsOptimistic设为false时,不需要覆写此方法
        /// </summary>
        public virtual void ApplyLocal() { }

        protected internal virtual void OnResponse() { }
        protected internal virtual void OnError() { }

        private UniTaskCompletionSource<bool> _tcs;

        /// <summary>
        /// 请求完成的 UniTask，返回 IsOk。仅在需要等待完成时机时访问（懒分配 TCS）。
        /// 大多数 fire-and-forget 场景不应访问此属性。
        /// </summary>
        public UniTask<bool> Completion => (_tcs ??= new UniTaskCompletionSource<bool>()).Task;

        /// <summary>
        /// 由 HttpModule 在 OnResponse/OnError 之后调用。
        /// 无 TCS 时空操作（fire-and-forget 路径零开销）。
        /// </summary>
        internal void SetCompleted()
        {
            _tcs?.TrySetResult(IsOk);
            _tcs = null;
        }
    }

    public abstract class HttpQueueEntity<TResp> : HttpQueueEntity
    {
        public TResp RespData { get; protected set; }
    }

    public abstract class HttpQueueEntity<TReq, TResp> : HttpQueueEntity<TResp>
    {
        public abstract TReq RqtData { get; }
    }
}
