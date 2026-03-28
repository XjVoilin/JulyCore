using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core.Config;
using JulyCore.Module.Analytics;
using JulyCore.Module.Data;
using JulyCore.Module.Fsm;
using JulyCore.Module.Http;
using JulyCore.Module.Performance;
using JulyCore.Module.Platform;
using JulyCore.Module.Pool;
using JulyCore.Module.Time;
using JulyCore.Provider.Data;
using JulyCore.Provider.Encryption;
using JulyCore.Provider.Fsm;
using JulyCore.Provider.Http;
using JulyCore.Provider.Performance;
using JulyCore.Provider.Platform;
using JulyCore.Provider.Pool;
using JulyCore.Provider.Time;

namespace JulyCore.Core
{
    /// <summary>
    /// 框架上下文
    /// 统一管理所有框架服务，提供依赖注入容器
    /// </summary>
    internal class FrameworkContext
    {
        private IServiceRegistry _registry;
        private IModuleService _moduleService;
        private IProviderService _providerService;
        private IEventBus _eventBus;
        private FrameworkConfig _frameworkConfig;
        private bool _isInitialized;

        internal static FrameworkContext _instance;
        public static FrameworkContext Instance => _instance;

        /// <summary>
        /// 服务注册表（DI）
        /// </summary>
        public IServiceRegistry Registry => _registry;

        /// <summary>
        /// 模块服务
        /// </summary>
        public IModuleService ModuleService => _moduleService;

        /// <summary>
        /// Provider 生命周期管理服务
        /// </summary>
        public IProviderService ProviderService => _providerService;

        /// <summary>
        /// 事件总线
        /// </summary>
        public IEventBus EventBus => _eventBus;
        
        /// <summary>
        /// 框架配置
        /// </summary>
        internal FrameworkConfig FrameworkConfig => _frameworkConfig;
        
        public CancellationToken CancellationToken { get; private set; }

        internal FrameworkContext(FrameworkConfig frameworkConfig)
        {
            _frameworkConfig = frameworkConfig;
            InitializeServices();
            RegisterDefaultModules();
            RegisterDefaultProviders();
        }

        internal void RegisterProvider<T>(T provider) where T : IProvider
        {
            _registry.Register<T>(provider);
            _providerService.Track(provider);
        }

        internal void ReplaceProvider<T>(T newProvider) where T : IProvider
        {
            if (_registry.TryResolve<T>(out var old) && old is IProvider oldProvider)
                _providerService.Untrack(oldProvider);
            _registry.Register<T>(newProvider);
            _providerService.Track(newProvider);
        }

        private void RegisterDefaultModules()
        {
            _moduleService.RegisterModule<TimeModule>();
            _moduleService.RegisterModule<SerializeModule>();
            _moduleService.RegisterModule<PoolModule>();
            _moduleService.RegisterModule<FsmModule>();
            _moduleService.RegisterModule<HttpModule>();
            _moduleService.RegisterModule<PlatformModule>();
            _moduleService.RegisterModule<PerformanceModule>();
            _moduleService.RegisterModule<AnalyticsModule>();
        }

        private void RegisterDefaultProviders()
        {
            RegisterProvider<ITimeProvider>(new UnityTimeProvider());
            RegisterProvider<ISerializeProvider>(new JsonSerializeProvider());
            RegisterProvider<IPoolProvider>(new PoolProvider());
            RegisterProvider<IFsmProvider>(new FsmProvider());
            RegisterProvider<IHttpProvider>(new HttpProvider());
            RegisterProvider<IPlatformProvider>(new DefaultPlatformProvider());
            RegisterProvider<IPerformanceProvider>(new UnityPerformanceProvider());
            RegisterProvider<IEncryptionProvider>(new NoEncryptionProvider());
        }

        /// <summary>
        /// 初始化所有服务
        /// </summary>
        private void InitializeServices()
        {
            _registry = new ServiceRegistry();

            _moduleService = new ModuleService();
            _providerService = new ProviderService();
            _eventBus = new EventBus(_frameworkConfig.EventBusConfig);

            _registry.Register<IServiceRegistry>(_registry);
            _registry.Register<IModuleService>(_moduleService);
            _registry.Register<IProviderService>(_providerService);
            _registry.Register<IEventBus>(_eventBus);
            _registry.Register(_frameworkConfig);
        }

        /// <summary>
        /// 初始化所有未初始化的 Provider（支持多次调用，已初始化的会被跳过）
        /// </summary>
        public async UniTask InitProvidersAsync(CancellationToken cancellationToken = default)
        {
            CancellationToken = cancellationToken;
            await _providerService.InitAllAsync();
        }

        /// <summary>
        /// 初始化并启用所有未就绪的 Module（支持多次调用，已初始化/启用的会被跳过）
        /// </summary>
        public async UniTask InitModulesAsync()
        {
            await _moduleService.InitAllAsync();
            _isInitialized = true;
        }

        /// <summary>
        /// 关闭框架
        /// </summary>
        public async UniTask ShutdownAsync()
        {
            if (!_isInitialized)
            {
                return;
            }

            JLogger.Log($"{Frameworkconst.TagFrameworkContext} 开始关闭框架");

            await _moduleService.ShutdownAsync();

            // 关闭所有Provider
            _providerService.ShutdownAll();

            _isInitialized = false;
            CancellationToken = CancellationToken.None;
            JLogger.Log($"{Frameworkconst.TagFrameworkContext} 框架已关闭");
        }

        /// <summary>
        /// 更新框架（调用所有Module的Update）
        /// </summary>
        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            if (!_isInitialized)
            {
                return;
            }

            // 更新所有模块
            _moduleService.Update(elapseSeconds, realElapseSeconds);
            
            // 处理事件总线延迟动作（帧分片机制）
            _eventBus.ProcessDeferredActions();
        }

        /// <summary>
        /// 清除所有服务（用于测试或重置）
        /// </summary>
        private void Clear()
        {
            _moduleService?.Clear();
            _providerService?.Clear();
            _eventBus?.Clear();
            _registry?.Clear();
            _isInitialized = false;
        }

        /// <summary>
        /// 重置框架上下文（用于测试）
        /// </summary>
        public static void Reset()
        {
            if (_instance != null)
            {
                _instance.Clear();
                _instance = null;
            }
        }
    }
}
