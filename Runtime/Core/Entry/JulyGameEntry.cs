using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Module.Analytics;
using JulyCore.Module.Audio;
using JulyCore.Module.Data;
using JulyCore.Module.Localization;
using JulyCore.Module.Performance;
using JulyCore.Module.Pool;
using JulyCore.Module.Save;
using JulyCore.Module.Resource;
using JulyCore.Module.Time;
using JulyCore.Module.UI;
using JulyCore.Provider.Audio;
using JulyCore.Provider.Data;
using JulyCore.Provider.Encryption;
using JulyCore.Provider.Localization;
using JulyCore.Provider.Performance;
using JulyCore.Provider.Pool;
using JulyCore.Provider.Resource;
using JulyCore.Provider.Save;
using JulyCore.Provider.Time;
using JulyCore.Provider.UI;
using JulyCore.Module.Task;
using JulyCore.Module.RedDot;
using JulyCore.Module.ABTest;
using JulyCore.Provider.Task;
using JulyCore.Provider.RedDot;
using JulyCore.Provider.ABTest;
using JulyCore.Provider.Guide;
using JulyCore.Module.Guide;
using JulyCore.Core.Config;
using JulyCore.Module.Activity;
using JulyCore.Module.Config;
using JulyCore.Module.Fsm;
using JulyCore.Module.Http;
using JulyCore.Module.Platform;
using JulyCore.Module.Scene;
using JulyCore.Provider.Activity;
using JulyCore.Provider.Analytics;
using JulyCore.Provider.Config;
using JulyCore.Provider.Fsm;
using JulyCore.Provider.Platform;
using JulyCore.Provider.Scene;
using UnityEngine;

namespace JulyCore.Core
{
    /// <summary>
    /// 游戏入口基类
    /// 负责框架的两阶段启动和生命周期管理：
    /// Phase 1: 基础 Module/Provider — 热更前初始化，提供 GF 基础能力
    /// Phase 2: 业务 Module/Provider — 热更后初始化，支持热更替换
    /// </summary>
    public abstract class JulyGameEntry : MonoBehaviour
    {
        [Header("框架配置文件")]
        [SerializeField]
        protected FrameworkConfig frameworkConfig;

        private bool _isInit;
        private CancellationTokenSource _cancellationTokenSource;
        
        private FrameworkContext _context;
        
        private readonly List<System.Type> _registeredProviderTypes = new();

        private void Awake()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            LaunchFramework().Forget();
            DontDestroyOnLoad(gameObject);
        }

        private async UniTask LaunchFramework()
        {
            try
            {
                _context = FrameworkContext._instance = new FrameworkContext(frameworkConfig);
                
                // Phase 0: 框架启动前的预处理（远程配置、强更检查等）
                var shouldContinue = await OnPreLaunch();
                if (!shouldContinue)
                {
                    JLogger.Log($"{Frameworkconst.TagJulyGameEntry} 框架启动被 OnPreLaunch 中止");
                    return;
                }
                
                JLogger.InitLogChannels(frameworkConfig.EnabledLogChannels);
                
                // Phase 1: 基础能力 — 注册并初始化基础 Module/Provider
                var moduleService = _context.ModuleService;
                RegisterDefaultBaseModules(moduleService);
                
                RegisterDefaultBaseProviders();
                OnConfigureBase();
                
                ResolveAllProviders();
                await _context.InitProvidersAsync(_cancellationTokenSource.Token);
                await _context.InitModulesAsync();
                
                _isInit = true;
                JLogger.Log($"{Frameworkconst.TagJulyGameEntry} 基础能力初始化完成，GF 基础 API 就绪");
                
                // Phase 2: 热更 + 业务能力（由 InnerInit 驱动）
                await InnerInit();
                
                JLogger.Log($"{Frameworkconst.TagJulyGameEntry} 框架启动完成");
            }
            catch (System.Exception ex)
            {
                JLogger.LogError($"{Frameworkconst.TagJulyGameEntry} 框架启动失败: {ex.Message}");
                JLogger.LogException(ex);
                throw;
            }
        }
        
        /// <summary>
        /// 框架启动前的预处理钩子。在任何 Provider/Module 注册和初始化之前调用。
        /// 适用于：远程配置拉取、强制更新检查、日志级别设置等不依赖框架的操作。
        /// 返回 true 继续启动框架，返回 false 中止启动（如需强制更新）。
        /// </summary>
        protected virtual UniTask<bool> OnPreLaunch() => UniTask.FromResult(true);

        #region Phase 1: 基础能力

        /// <summary>
        /// 注册框架默认基础 Module（热更前初始化，提供核心能力）
        /// </summary>
        private void RegisterDefaultBaseModules(IModuleService moduleService)
        {
            moduleService.RegisterModule<ResourceModule>();
            moduleService.RegisterModule<TimeModule>();
            moduleService.RegisterModule<SerializeModule>();
            moduleService.RegisterModule<FsmModule>();
            moduleService.RegisterModule<PoolModule>();
            moduleService.RegisterModule<PerformanceModule>();
            moduleService.RegisterModule<SceneModule>();
            moduleService.RegisterModule<PlatformModule>();
            moduleService.RegisterModule<HttpModule>();
        }

        /// <summary>
        /// 注册框架默认基础 Provider（热更前初始化）
        /// </summary>
        private void RegisterDefaultBaseProviders()
        {
            RegisterProviderType<ISerializeProvider, JsonSerializeProvider>();
            RegisterProviderType<IEncryptionProvider, AesEncryptionProvider>();
            RegisterProviderType<IResourceProvider, UnityResourceProvider>();
            RegisterProviderType<IPoolProvider, PoolProvider>();
            RegisterProviderType<ITimeProvider, UnityTimeProvider>();
            RegisterProviderType<IPerformanceProvider, UnityPerformanceProvider>();
            RegisterProviderType<IFsmProvider, FsmProvider>();
            RegisterProviderType<IPlatformProvider, NullPlatformProvider>();
        }

        /// <summary>
        /// 项目配置基础层钩子（子类重写）。
        /// 时机：框架默认基础 Provider 注册后、初始化前。
        /// 典型用途：注册 YooAsset、平台 SDK 等 AOT Provider 覆盖默认实现。
        /// </summary>
        protected virtual void OnConfigureBase()
        {
        }

        #endregion

        #region Phase 2: 业务能力

        /// <summary>
        /// 注册框架默认业务 Module/Provider。
        /// 供 GameEntryBase 在热更后、初始化前调用。
        /// </summary>
        protected void RegisterBusinessDefaults()
        {
            var moduleService = _context.ModuleService;
            RegisterDefaultBusinessModules(moduleService);
            RegisterBusinessModules(moduleService);
            
            RegisterDefaultBusinessProviders();
        }

        /// <summary>
        /// 解析并初始化所有待处理的 Provider 和 Module。
        /// 供 GameEntryBase 在热更注册器完成后调用。
        /// 已初始化的 Provider/Module 会被自动跳过。
        /// </summary>
        protected async UniTask InitPendingAsync()
        {
            ResolveAllProviders();
            await _context.InitProvidersAsync(_cancellationTokenSource.Token);
            await _context.InitModulesAsync();
            
            JLogger.Log($"{Frameworkconst.TagJulyGameEntry} 业务能力初始化完成");
        }

        /// <summary>
        /// 注册框架默认业务 Module（热更后初始化，支持热更替换 Provider）
        /// </summary>
        private void RegisterDefaultBusinessModules(IModuleService moduleService)
        {
            moduleService.RegisterModule<LocalizationModule>();
            moduleService.RegisterModule<UIModule>();
            moduleService.RegisterModule<AudioModule>();
            moduleService.RegisterModule<SaveModule>();
            moduleService.RegisterModule<ConfigModule>();
            moduleService.RegisterModule<AnalyticsModule>();
            moduleService.RegisterModule<ABTestModule>();
            moduleService.RegisterModule<TaskModule>();
            moduleService.RegisterModule<RedDotModule>();
            moduleService.RegisterModule<GuideModule>();
            moduleService.RegisterModule<ActivityModule>();
        }

        /// <summary>
        /// 注册框架默认业务 Provider（热更后初始化）
        /// </summary>
        private void RegisterDefaultBusinessProviders()
        {
            RegisterProviderType<IAnalyticsProvider, NullAnalyticsProvider>();
            RegisterProviderType<IConfigProvider, ConfigProvider>();
            RegisterProviderType<IUIProvider, UIProvider>();
            RegisterProviderType<IAudioProvider, UnityAudioProvider>();
            RegisterProviderType<ISaveProvider, LocalFileSaveProvider>();
            RegisterProviderType<ILocalizationProvider, LocalizationProvider>();
            RegisterProviderType<IABTestProvider, ABTestProvider>();
            RegisterProviderType<ITaskProvider, TaskProvider>();
            RegisterProviderType<IRedDotProvider, RedDotProvider>();
            RegisterProviderType<IGuideProvider, GuideProvider>();
            RegisterProviderType<IActivityProvider, SavedActivityProvider>();
        }

        /// <summary>
        /// 注册项目自定义业务 Module（子类重写）
        /// </summary>
        protected virtual void RegisterBusinessModules(IModuleService moduleService)
        {
        }

        #endregion

        #region Provider 注册工具

        /// <summary>
        /// 注册 Provider 类型（仅注册到 DI 容器，不立即解析）
        /// </summary>
        protected void RegisterProvider<TInterface, TImplementation>()
            where TInterface : IProvider
            where TImplementation : class, TInterface
        {
            RegisterProviderType<TInterface, TImplementation>();
        }

        private void RegisterProviderType<TInterface, TImplementation>()
            where TInterface : IProvider
            where TImplementation : class, TInterface
        {
            var interfaceType = typeof(TInterface);
            
            if (!_registeredProviderTypes.Contains(interfaceType))
            {
                _registeredProviderTypes.Add(interfaceType);
            }
            
            _context.Container.RegisterSingleton<TInterface, TImplementation>();
        }

        /// <summary>
        /// 向 DI 容器注册非 Provider 单例实例，供 Provider 构造函数注入使用。
        /// 必须在 Provider 解析之前调用（通常在 OnPreLaunch 中）。
        /// </summary>
        protected void RegisterSingleton<T>(T instance)
        {
            _context.Container.RegisterSingleton(instance);
        }

        /// <summary>
        /// 解析所有已注册的 Provider 并追踪生命周期（Track 内部去重）
        /// </summary>
        private void ResolveAllProviders()
        {
            foreach (var interfaceType in _registeredProviderTypes)
            {
                if (_context.Container.Resolve(interfaceType) is IProvider provider)
                {
                    _context.ProviderService.Track(provider);
                }
            }
        }

        #endregion

        #region 生命周期

        protected virtual void Update()
        {
            if (!_isInit)
                return;

            var elapseSeconds = Time.deltaTime;
            var realElapseSeconds = Time.unscaledDeltaTime;
            FrameworkContext.Instance.Update(elapseSeconds, realElapseSeconds);
        }

        private void OnDestroy()
        {
            ShutdownFramework().Forget();
        }

        private async UniTask ShutdownFramework()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                await FrameworkContext.Instance.ShutdownAsync();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                JLogger.Log($"{Frameworkconst.TagJulyGameEntry} 框架已关闭");
            }
            catch (System.Exception ex)
            {
                JLogger.LogError($"{Frameworkconst.TagJulyGameEntry} 框架关闭失败: {ex.Message}");
                JLogger.LogException(ex);
            }
        }

        /// <summary>
        /// 从 DI 容器解析已初始化的 Provider 实例
        /// </summary>
        protected T ResolveProvider<T>() where T : IProvider
        {
            return _context.Container.Resolve<T>();
        }

        /// <summary>
        /// 初始化游戏逻辑（子类实现）
        /// </summary>
        protected abstract UniTask InnerInit();

        #endregion
    }
}
