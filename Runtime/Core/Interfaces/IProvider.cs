using Cysharp.Threading.Tasks;

namespace JulyCore.Core
{
    /// <summary>
    /// 提供 底层能力 / 系统级服务。
    /// 外部项目可实现此接口来创建自定义 Provider。
    /// </summary>
    public interface IProvider
    {
        /// <summary>
        /// Provider名称，用于日志和调试
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 是否已初始化
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// 初始化Provider
        /// 框架启动时调用，用于初始化底层能力
        /// 使用框架级 CancellationToken（从 FrameworkContext 获取）
        /// </summary>
        /// <returns>初始化任务</returns>
        UniTask InitAsync();

        /// <summary>
        /// 关闭Provider
        /// 框架关闭时调用，用于清理资源但保留实例
        /// 使用框架级 CancellationToken（从 FrameworkContext 获取）
        /// </summary>
        /// <returns>关闭任务</returns>
        UniTask ShutdownAsync();
    }
}