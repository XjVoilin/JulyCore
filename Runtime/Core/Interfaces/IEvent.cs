namespace JulyCore.Core
{
    /// <summary>
    /// 事件接口标记
    /// 所有事件类型都应实现此接口，提供类型安全和语义明确性
    /// </summary>
    public interface IEvent
    {
        // 标记接口，不包含任何成员
        // 用于类型约束，确保只有明确标记为事件的对象才能被发布
    }
}

