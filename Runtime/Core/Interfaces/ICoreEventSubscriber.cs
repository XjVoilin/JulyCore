using System;

namespace JulyCore.Core
{
    /// <summary>
    /// Core 层事件只读订阅接口。
    /// 上层通过 GF.CoreEvent 获取此接口，仅能订阅基础设施事件（Scene/Network/Audio 等），
    /// 不能向 Core 总线发布事件。业务事件请使用 this.Subscribe() / this.Publish() 通过 ArchContext 总线。
    /// </summary>
    public interface ICoreEventSubscriber
    {
        void Subscribe<T>(Action<T> handler, object owner);
        void Unsubscribe<T>(Action<T> handler);
        void UnsubscribeAll(object owner);
    }
}
