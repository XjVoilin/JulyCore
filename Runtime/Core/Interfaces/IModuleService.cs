using Cysharp.Threading.Tasks;

namespace JulyCore.Core
{
    /// <summary>
    /// 模块服务接口
    /// 负责 Module 的注册、获取和生命周期管理
    /// </summary>
    public interface IModuleService
    {
        /// <summary>
        /// 是否已初始化
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// 注册模块
        /// </summary>
        /// <typeparam name="T">Module类型</typeparam>
        /// <exception cref="JulyException">当Module已注册时抛出</exception>
        void RegisterModule<T>() where T : IModule, new();

        /// <summary>
        /// 注册模块（使用实例）
        /// </summary>
        /// <param name="module">模块实例</param>
        /// <exception cref="JulyException">当Module已注册时抛出</exception>
        void RegisterModule(IModule module);

        /// <summary>
        /// 获取模块
        /// </summary>
        /// <typeparam name="T">Module类型</typeparam>
        /// <returns>Module实例，如果未注册则返回null</returns>
        T GetModule<T>() where T : IModule;

        /// <summary>
        /// 尝试获取模块
        /// </summary>
        /// <typeparam name="T">Module类型</typeparam>
        /// <param name="module">输出的Module实例</param>
        /// <returns>是否获取成功</returns>
        bool TryGetModule<T>(out T module) where T : IModule;

        /// <summary>
        /// 通过接口类型获取实现了该接口的模块
        /// 用于 Capability 接口模式，允许 Module 之间通过能力接口通信
        /// </summary>
        /// <typeparam name="TCapability">能力接口类型（必须继承 ICapability）</typeparam>
        /// <returns>实现了该接口的 Module，如果未找到则返回 null</returns>
        TCapability GetModuleByCapability<TCapability>() where TCapability : class, ICapability;

        /// <summary>
        /// 是否存在模块
        /// </summary>
        /// <typeparam name="T">Module类型</typeparam>
        /// <returns>是否存在</returns>
        bool HasModule<T>() where T : IModule;

        /// <summary>
        /// 初始化并启用所有 Module（自动解析依赖关系，已初始化的跳过）
        /// </summary>
        UniTask InitAllAsync();

        /// <summary>
        /// 关闭所有 Module
        /// </summary>
        void Shutdown();

        /// <summary>
        /// 更新所有 Module（只更新已启用的Module）
        /// </summary>
        /// <param name="elapseSeconds">游戏时间流逝（秒）</param>
        /// <param name="realElapseSeconds">真实时间流逝（秒）</param>
        void Update(float elapseSeconds, float realElapseSeconds);

        /// <summary>
        /// 清空所有 Module（用于测试或重置）
        /// </summary>
        void Clear();
    }
}
