using System;
using Cysharp.Threading.Tasks;

namespace JulyCore.Module.Http
{
    /// <summary>
    /// 队列请求基类。Send(HttpQueueEntity) 入队即返回（void），由队列后台处理。
    /// 与 HttpEntity 平行，编译器强制隔离两条发送路径。
    /// 需要感知完成时机时，调用方可 await entity.Completion（懒分配 TCS，fire-and-forget 路径零开销）。
    /// </summary>
    public abstract class HttpQueueEntity : HttpEntityBase
    {
        public string RequestId { get; private set; } = Guid.NewGuid().ToString("N");

        public void RegenerateRequestId() => RequestId = Guid.NewGuid().ToString("N");

        public virtual bool IsBlocking => false;

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
