using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Core.Config;

namespace JulyCore.Module.Base
{
    /// <summary>
    /// Module抽象基类
    /// 提供通用的生命周期管理和状态跟踪
    /// 外部项目可继承此类来创建自定义模块
    /// </summary>
    public abstract class ModuleBase : IModule, IPriority
    {
        private bool _isInitialized = false;
        private bool _isEnabled = false;
        private bool _isDisposed = false;
        private bool _isShuttingDown = false;
        private FrameworkContext _context;

        /// <summary>
        /// Module名称，用于日志和调试
        /// </summary>
        public string Name => GetType().Name;

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// 是否已启用
        /// </summary>
        public bool IsEnabled => _isEnabled;

        /// <summary>
        /// 模块执行优先级（数值越小优先级越高）
        /// </summary>
        public virtual int Priority => 0;
        
        /// <summary>
        /// 模块所属的日志通道（子类必须重写以指定通道）
        /// </summary>
        protected abstract LogChannel LogChannel { get; }

        protected CancellationToken GFCancellationToken => _context.CancellationToken;

        #region 受控服务访问（子类通过这些属性访问框架服务）

        /// <summary>
        /// 事件总线（用于发布/订阅事件）
        /// </summary>
        protected IEventBus EventBus => _context?.EventBus;
        
        protected FrameworkConfig FrameworkConfig => _context.FrameworkConfig;

        /// <summary>
        /// 获取 Provider（通过 DI 容器）
        /// </summary>
        protected T GetProvider<T>() where T : IProvider
        {
            var result = _context.Container.Resolve<T>();
            if (result == null)
            {
                var type = typeof(T);
                throw new JulyException($"[{Name}] 需要{type}，请先注册");
            }
            return result;
        }

        /// <summary>
        /// 获取 Capability（通过能力接口访问其他 Module 的功能）
        /// 
        /// 【设计说明】
        /// Module 之间不能直接相互引用，但可以通过 Capability 接口访问其他 Module 暴露的能力。
        /// 例如：TaskModule 通过 ITimeCapability 访问 TimeModule 的时间功能。
        /// 
        /// 【注意事项】
        /// - Capability 接口由提供能力的 Module 实现（如 TimeModule 实现 ITimeCapability）
        /// - 只能访问通过 Capability 接口暴露的方法，不能访问 Module 的全部功能
        /// - 这是一种受控的 Module 间通信方式
        /// - TCapability 必须继承 ICapability 接口
        /// </summary>
        /// <typeparam name="TCapability">能力接口类型（如 ITimeCapability），必须继承 ICapability</typeparam>
        /// <returns>实现了该能力接口的 Module</returns>
        /// <exception cref="JulyException">当未找到实现该能力的 Module 时抛出</exception>
        protected TCapability GetCapability<TCapability>() where TCapability : class, ICapability
        {
            var result = _context.ModuleService.GetModuleByCapability<TCapability>();
            if (result == null)
            {
                var type = typeof(TCapability);
                throw new JulyException($"[{Name}] 需要能力 {type.Name}，请确保对应的 Module 已注册并实现了该接口");
            }
            return result;
        }

        // 注意：Module 之间不能直接相互引用，需要通过 Capability 接口、Provider 或 EventBus 通信

        #endregion

        #region 模块日志方法

        /// <summary>
        /// 输出模块普通日志（自动带模块名前缀，受日志通道开关控制）
        /// </summary>
        protected void Log(object message)
        {
            JLogger.LogChannel(LogChannel, Name, message);
        }

        /// <summary>
        /// 输出模块警告日志（不受通道开关控制）
        /// </summary>
        protected void LogWarning(object message)
        {
            JLogger.LogChannelWarning(Name, message);
        }

        /// <summary>
        /// 输出模块错误日志（不受通道开关控制）
        /// </summary>
        protected void LogError(object message)
        {
            JLogger.LogChannelError(Name, message);
        }

        /// <summary>
        /// 检查当前模块的普通日志是否启用
        /// </summary>
        protected bool IsLogEnabled => JLogger.IsChannelEnabled(LogChannel);

        #endregion

        /// <summary>
        /// 初始化Module
        /// 功能逻辑,调用Provider
        /// </summary>
        /// <returns>初始化任务</returns>
        public async UniTask InitAsync()
        {
            if (_isInitialized)
            {
                JLogger.LogWarning($"[{Name}] Module已经初始化，跳过重复初始化");
                return;
            }

            if (_isDisposed)
            {
                throw new JulyException($"[{Name}] Module已被释放，无法初始化");
            }

            try
            {
                // 缓存框架上下文
                _context = FrameworkContext.Instance;

                JLogger.Log($"[{Name}] 开始初始化Module");
                await OnInitAsync();
                _isInitialized = true;
                JLogger.Log($"[{Name}] Module初始化完成");
            }
            catch (Exception ex)
            {
                JLogger.LogError($"[{Name}] Module初始化失败: {ex.Message}");
                JLogger.LogException(ex);
                throw;
            }
        }

        /// <summary>
        /// 启用Module
        /// </summary>
        /// <returns>启用任务</returns>
        public async UniTask EnableAsync()
        {
            if (!_isInitialized)
            {
                throw new JulyException($"[{Name}] Module未初始化，无法启用");
            }

            if (_isEnabled)
            {
                JLogger.LogWarning($"[{Name}] Module已经启用，跳过重复启用");
                return;
            }

            try
            {
                JLogger.Log($"[{Name}] 开始启用Module");
                await OnEnableAsync();
                _isEnabled = true;
                JLogger.Log($"[{Name}] Module启用完成");
            }
            catch (Exception ex)
            {
                JLogger.LogError($"[{Name}] Module启用失败: {ex.Message}");
                JLogger.LogException(ex);
                throw;
            }
        }

        /// <summary>
        /// 禁用Module
        /// </summary>
        /// <returns>禁用任务</returns>
        public async UniTask DisableAsync()
        {
            if (!_isEnabled)
            {
                return;
            }

            try
            {
                JLogger.Log($"[{Name}] 开始禁用Module");
                await OnDisableAsync();
                _isEnabled = false;
                JLogger.Log($"[{Name}] Module禁用完成");
            }
            catch (Exception ex)
            {
                JLogger.LogError($"[{Name}] Module禁用失败: {ex.Message}");
                JLogger.LogException(ex);
                throw;
            }
        }

        /// <summary>
        /// 模块轮询（只更新已启用的Module）
        /// </summary>
        /// <param name="elapseSeconds">游戏时间流逝（秒）</param>
        /// <param name="realElapseSeconds">真实时间流逝（秒）</param>
        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            if (!_isInitialized || !_isEnabled || _isDisposed)
            {
                return;
            }

            OnUpdate(elapseSeconds, realElapseSeconds);
        }

        /// <summary>
        /// 关闭Module
        /// </summary>
        /// <returns>关闭任务</returns>
        public async UniTask ShutdownAsync()
        {
            if (!_isInitialized || _isShuttingDown)
            {
                return;
            }

            _isShuttingDown = true;
            try
            {
                // 先禁用
                if (_isEnabled)
                {
                    await DisableAsync();
                }

                JLogger.Log($"[{Name}] 开始关闭Module");
                await OnShutdownAsync();
                _isInitialized = false;
                JLogger.Log($"[{Name}] Module关闭完成");
            }
            catch (Exception ex)
            {
                JLogger.LogError($"[{Name}] Module关闭失败: {ex.Message}");
                JLogger.LogException(ex);
                throw;
            }
            finally
            {
                _isShuttingDown = false;
            }
        }

        /// <summary>
        /// 释放资源。
        /// 调用方应先 await ShutdownAsync() 再调用 Dispose()。
        /// 如果未提前 Shutdown，此方法仅同步释放资源，不会阻塞等待异步关闭。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            try
            {
                if (_isInitialized && !_isShuttingDown)
                {
                    JLogger.LogWarning($"[{Name}] Module 在未 Shutdown 的情况下被 Dispose，" +
                        "请确保调用方先 await ShutdownAsync()。仅执行同步释放。");
                }

                OnDispose();
                _isDisposed = true;
                JLogger.Log($"[{Name}] Module已释放");
            }
            catch (Exception ex)
            {
                JLogger.LogError($"[{Name}] Module释放失败: {ex.Message}");
                JLogger.LogException(ex);
            }
        }

        /// <summary>
        /// 子类实现：具体的初始化逻辑
        /// </summary>
        protected virtual UniTask OnInitAsync()
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 子类实现：具体的启用逻辑
        /// </summary>
        protected virtual UniTask OnEnableAsync()
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 子类实现：具体的禁用逻辑
        /// </summary>
        protected virtual UniTask OnDisableAsync()
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 子类实现：具体的更新逻辑
        /// </summary>
        protected virtual void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
        }

        /// <summary>
        /// 子类实现：具体的关闭逻辑
        /// </summary>
        protected virtual UniTask OnShutdownAsync()
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 子类实现：具体的释放逻辑
        /// </summary>
        protected virtual void OnDispose()
        {
        }
    }
}
