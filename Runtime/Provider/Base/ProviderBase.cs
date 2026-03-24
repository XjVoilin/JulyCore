using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;

namespace JulyCore.Provider.Base
{
    /// <summary>
    /// Provider 抽象基类
    /// 提供通用的生命周期管理和状态跟踪
    /// 
    /// 【依赖注入】
    /// Provider 之间的依赖通过构造函数注入：
    /// public class UIProvider : ProviderBase, IUIProvider
    /// {
    ///     private readonly IResourceProvider _resourceProvider;
    ///     public UIProvider(IResourceProvider resourceProvider)
    ///     {
    ///         _resourceProvider = resourceProvider;
    ///     }
    /// }
    /// </summary>
    public abstract class ProviderBase : IProvider, IPriority
    {
        private bool _isInitialized = false;

        /// <summary>
        /// Provider 名称，用于日志和调试
        /// </summary>
        public virtual string Name => GetType().Name;

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Provider 优先级（数值越小优先级越高，越先初始化）
        /// 默认值为 0
        /// </summary>
        public virtual int Priority => 0;

        /// <summary>
        /// Provider 所属的日志通道（子类必须重写以指定通道）
        /// </summary>
        protected abstract LogChannel LogChannel { get; }
        
        /// <summary>
        /// 框架取消令牌
        /// </summary>
        protected CancellationToken CancellationToken => FrameworkContext.Instance.CancellationToken;

        /// <summary>
        /// 初始化 Provider
        /// </summary>
        public async UniTask InitAsync()
        {
            if (_isInitialized)
            {
                JLogger.LogWarning($"[{Name}] Provider 已初始化，跳过");
                return;
            }

            await OnInitAsync();
            _isInitialized = true;
        }

        /// <summary>
        /// 关闭 Provider
        /// </summary>
        public void Shutdown()
        {
            if (!_isInitialized) return;
            _isInitialized = false;
            OnShutdown();
        }

        /// <summary>
        /// 子类实现：具体的初始化逻辑
        /// </summary>
        protected virtual UniTask OnInitAsync() => UniTask.CompletedTask;

        /// <summary>
        /// 子类实现：具体的关闭逻辑
        /// </summary>
        protected virtual void OnShutdown() { }

        #region Provider 日志方法

        /// <summary>
        /// 输出普通日志（受日志通道开关控制）
        /// </summary>
        protected void Log(object message)
        {
            JLogger.LogChannel(LogChannel, Name, message);
        }

        /// <summary>
        /// 输出警告日志（不受通道开关控制）
        /// </summary>
        protected void LogWarning(object message)
        {
            JLogger.LogChannelWarning(Name, message);
        }

        /// <summary>
        /// 输出错误日志（不受通道开关控制）
        /// </summary>
        protected void LogError(object message)
        {
            JLogger.LogChannelError(Name, message);
        }

        /// <summary>
        /// 检查当前 Provider 的普通日志是否启用
        /// </summary>
        protected bool IsLogEnabled => JLogger.IsChannelEnabled(LogChannel);

        #endregion
    }
}
