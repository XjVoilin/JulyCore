using Cysharp.Threading.Tasks;

namespace JulyCore.Core
{
    /// <summary>
    /// Provider服务接口
    /// 仅负责 Provider 的生命周期管理（Init/Shutdown）
    /// Provider 的注册和获取通过 IDependencyContainer 统一处理
    /// </summary>
    public interface IProviderService
    {
        /// <summary>
        /// 是否已初始化
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// 追踪 Provider 的生命周期（由框架内部调用）
        /// </summary>
        /// <param name="provider">Provider 实例</param>
        void Track(IProvider provider);

        /// <summary>
        /// 初始化所有已追踪的 Provider
        /// </summary>
        UniTask InitAllAsync();

        /// <summary>
        /// 关闭所有已追踪的 Provider
        /// </summary>
        UniTask ShutdownAllAsync();

        /// <summary>
        /// 清空所有 Provider（用于测试或重置）
        /// </summary>
        void Clear();
    }
}
