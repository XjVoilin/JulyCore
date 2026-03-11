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
using JulyCore.Module.Platform;
using JulyCore.Module.Scene;
using JulyCore.Provider.Activity;
using JulyCore.Provider.Config;
using JulyCore.Provider.Fsm;
using JulyCore.Provider.Platform;
using JulyCore.Provider.Scene;
using UnityEngine;

namespace JulyCore.Core
{
    /// <summary>
    /// 游戏入口基类
    /// 负责框架的启动和生命周期管理
    /// </summary>
    public abstract class JulyGameEntry : MonoBehaviour
    {
        [Header("框架配置文件")]
        [SerializeField]
        protected FrameworkConfig frameworkConfig;

        private bool _isInit;
        private CancellationTokenSource _cancellationTokenSource;
        
        private FrameworkContext _context;
        
        // 记录所有注册的 Provider 接口类型（按注册顺序）
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
                
                // 初始化日志通道配置
                JLogger.InitLogChannels(frameworkConfig.EnabledLogChannels);
                
                // 【重要】先注册 Module（但不初始化）
                // Module 注册时会将其实现的 Capability 接口注册到 DI 容器
                // 这样 Provider 才能通过构造函数注入这些 Capability（如 ITimeCapability）
                var moduleService = _context.ModuleService;
                RegisterDefaultModules(moduleService);
                RegisterModules(moduleService);
                
                // 注册 Provider（通过 DI 容器，支持构造函数注入）
                // 先注册默认 Provider，再注册用户自定义 Provider（用户 Provider 覆盖默认）
                RegisterDefaultProviders();
                RegisterProviders();
                
                // 所有注册完成后，统一解析并初始化 Provider
                ResolveAllProviders();

                await _context.InitAsync(_cancellationTokenSource.Token);
                await InnerInit();
                _isInit = true;
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
        /// 注册框架默认 Provider（仅注册类型，不立即解析）
        /// </summary>
        private void RegisterDefaultProviders()
        {
            // === 第一层：无依赖的基础 Provider ===
            RegisterProviderType<ISerializeProvider, JsonSerializeProvider>();
            RegisterProviderType<IEncryptionProvider, AesEncryptionProvider>();
            RegisterProviderType<IResourceProvider, UnityResourceProvider>();
            RegisterProviderType<IPoolProvider, PoolProvider>();
            RegisterProviderType<ITimeProvider, UnityTimeProvider>();
            RegisterProviderType<IConfigProvider, ConfigProvider>();
            RegisterProviderType<IPerformanceProvider, UnityPerformanceProvider>();
            RegisterProviderType<IFsmProvider, FsmProvider>();
            
            // === 平台 SDK Provider ===
            RegisterProviderType<IPlatformProvider, NullPlatformProvider>();
            
            // === 第二层：依赖第一层 Provider ===
            RegisterProviderType<IUIProvider, UIProvider>();
            RegisterProviderType<IAudioProvider, UnityAudioProvider>();
            RegisterProviderType<ISaveProvider, LocalFileSaveProvider>();
            RegisterProviderType<ILocalizationProvider, LocalizationProvider>();
            
            // === 第三层：独立的业务 Provider ===
            RegisterProviderType<IABTestProvider, ABTestProvider>();
            RegisterProviderType<ITaskProvider, TaskProvider>();
            RegisterProviderType<IRedDotProvider, RedDotProvider>();
            RegisterProviderType<IGuideProvider, GuideProvider>();
            RegisterProviderType<IActivityProvider, SavedActivityProvider>();
        }

        /// <summary>
        /// 注册 Provider 类型（仅注册，不立即解析）
        /// 用户自定义 Provider 会覆盖默认 Provider
        /// </summary>
        protected void RegisterProvider<TInterface, TImplementation>()
            where TInterface : IProvider
            where TImplementation : class, TInterface
        {
            RegisterProviderType<TInterface, TImplementation>();
        }

        /// <summary>
        /// 内部方法：注册 Provider 类型到 DI 容器（仅注册，不解析）
        /// </summary>
        private void RegisterProviderType<TInterface, TImplementation>()
            where TInterface : IProvider
            where TImplementation : class, TInterface
        {
            var interfaceType = typeof(TInterface);
            
            // 记录接口类型（如果已存在则不重复添加，保持首次注册的顺序）
            if (!_registeredProviderTypes.Contains(interfaceType))
            {
                _registeredProviderTypes.Add(interfaceType);
            }
            
            _context.Container.RegisterSingleton<TInterface, TImplementation>();
        }

        /// <summary>
        /// 解析所有已注册的 Provider 并追踪生命周期
        /// 自动遍历所有注册过的 Provider 类型，无需手动维护列表
        /// </summary>
        private void ResolveAllProviders()
        {
            foreach (var interfaceType in _registeredProviderTypes)
            {
                var provider = _context.Container.Resolve(interfaceType) as IProvider;
                if (provider != null)
                {
                    _context.ProviderService.Track(provider);
                }
            }
        }

        /// <summary>
        /// 注册自定义 Provider（子类重写）
        /// 用户在此方法中注册的 Provider 会覆盖默认 Provider
        /// </summary>
        protected virtual void RegisterProviders()
        {
        }

        /// <summary>
        /// 注册框架默认 Module
        /// </summary>
        private void RegisterDefaultModules(IModuleService moduleService)
        {
            // moduleService.RegisterModule<HotUpdateModule>();
            moduleService.RegisterModule<ResourceModule>();
            moduleService.RegisterModule<TimeModule>();
            moduleService.RegisterModule<LocalizationModule>();
            moduleService.RegisterModule<SerializeModule>();
            moduleService.RegisterModule<FsmModule>();
            // moduleService.RegisterModule<NetworkModule>();
            moduleService.RegisterModule<PoolModule>();
            moduleService.RegisterModule<UIModule>();
            moduleService.RegisterModule<PerformanceModule>();
            moduleService.RegisterModule<SceneModule>();
            moduleService.RegisterModule<SaveModule>();
            moduleService.RegisterModule<PlatformModule>();
            moduleService.RegisterModule<AudioModule>();
            moduleService.RegisterModule<ConfigModule>();
            moduleService.RegisterModule<AnalyticsModule>();
            moduleService.RegisterModule<ABTestModule>();
            moduleService.RegisterModule<TaskModule>();
            moduleService.RegisterModule<RedDotModule>();
            moduleService.RegisterModule<GuideModule>();
            moduleService.RegisterModule<ActivityModule>();
        }

        /// <summary>
        /// 注册自定义 Module（子类重写）
        /// </summary>
        protected virtual void RegisterModules(IModuleService moduleService)
        {
        }

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
        /// 初始化游戏逻辑
        /// 子类实现具体的游戏初始化逻辑
        /// </summary>
        protected abstract UniTask InnerInit();
    }
}
