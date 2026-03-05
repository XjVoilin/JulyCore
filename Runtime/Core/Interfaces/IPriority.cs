namespace JulyCore.Core
{
    /// <summary>
    /// 优先级接口
    /// 实现此接口以提供优先级信息，避免使用反射
    /// </summary>
    public interface IPriority
    {
        /// <summary>
        /// 优先级（数值越小优先级越高）
        /// </summary>
        int Priority { get; }
    }
}

