using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace JulyCore.Core
{
    /// <summary>
    /// 游戏内的一个功能 / 系统 / 域逻辑。
    /// 外部项目可实现此接口来创建自定义模块。
    /// </summary>
    public interface IModule : IDisposable
    {
        /// <summary>
        /// Module名称，用于日志和调试
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 是否已初始化
        /// </summary>
        bool IsInitialized { get; }
        
        /// <summary>
        /// 是否已启用
        /// </summary>
        bool IsEnabled { get; }
        
        /// <summary>
        /// 模块执行优先级
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// 初始化Module
        /// 功能逻辑,调用Provider
        /// 使用框架级 CancellationToken（从 FrameworkContext 获取）
        /// </summary>
        /// <returns>初始化任务</returns>
        UniTask InitAsync();
        
        /// <summary>
        /// 模块轮询
        /// </summary>
        /// <param name="elapseSeconds"></param>
        /// <param name="realElapseSeconds"></param>
        void Update(float elapseSeconds, float realElapseSeconds);

        /// <summary>
        /// 启用Module
        /// 使用框架级 CancellationToken（从 FrameworkContext 获取）
        /// </summary>
        /// <returns>启用任务</returns>
        UniTask EnableAsync();
        
        /// <summary>
        /// 禁用Module
        /// 使用框架级 CancellationToken（从 FrameworkContext 获取）
        /// </summary>
        /// <returns>禁用任务</returns>
        UniTask DisableAsync();
        
        /// <summary>
        /// 关闭Module
        /// 使用框架级 CancellationToken（从 FrameworkContext 获取）
        /// </summary>
        /// <returns>关闭任务</returns>
        UniTask ShutdownAsync();
    }
}