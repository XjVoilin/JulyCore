using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core.Config;

namespace JulyCore.Core
{
    /// <summary>
    /// 框架上下文
    /// 统一管理所有框架服务，提供依赖注入容器
    /// </summary>
    internal class FrameworkContext
    {
        private IDependencyContainer _container;
        private IModuleService _moduleService;
        private IProviderService _providerService;
        private IEventBus _eventBus;
        private FrameworkConfig _frameworkConfig;
        private bool _isInitialized;

        internal static FrameworkContext _instance;
        public static FrameworkContext Instance => _instance;

        /// <summary>
        /// 依赖注入容器
        /// </summary>
        public IDependencyContainer Container => _container;

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
        }

        /// <summary>
        /// 初始化所有服务
        /// </summary>
        private void InitializeServices()
        {
            // 创建依赖注入容器
            _container = new DependencyContainer();

            // 创建核心服务
            _moduleService = new ModuleService();
            _providerService = new ProviderService();
            _eventBus = new EventBus(_frameworkConfig.EventBusConfig);
            
            // 设置 ModuleService 的容器引用（用于自动注册 Capability）
            ((ModuleService)_moduleService).SetContainer(_container);

            // 注册核心服务到容器
            _container.RegisterSingleton(_container);
            _container.RegisterSingleton(_moduleService);
            _container.RegisterSingleton(_providerService);
            _container.RegisterSingleton(_eventBus);
            _container.RegisterSingleton(_frameworkConfig);
        }

        /// <summary>
        /// 注册 Provider 到 DI 容器并追踪生命周期
        /// </summary>
        /// <typeparam name="TInterface">Provider 接口类型</typeparam>
        /// <typeparam name="TImplementation">Provider 实现类型</typeparam>
        public void RegisterProvider<TInterface, TImplementation>() 
            where TInterface : IProvider 
            where TImplementation : class, TInterface
        {
            // 注册到 DI 容器（使用构造函数注入）
            _container.RegisterSingleton<TInterface, TImplementation>();
            
            // 解析实例并追踪生命周期
            var provider = _container.Resolve<TInterface>();
            _providerService.Track(provider);
        }

        /// <summary>
        /// 注册已有实例的 Provider
        /// </summary>
        public void RegisterProvider<TInterface>(TInterface provider) where TInterface : IProvider
        {
            // 注册到 DI 容器
            _container.RegisterSingleton(provider);
            
            // 追踪生命周期
            _providerService.Track(provider);
        }

        /// <summary>
        /// 初始化框架
        /// </summary>
        public async UniTask InitAsync(CancellationToken cancellationToken = default)
        {
            if (_isInitialized)
            {
                JLogger.LogWarning($"{Frameworkconst.TagFrameworkContext} 框架已经初始化，跳过重复初始化");
                return;
            }

            try
            {
                JLogger.Log($"{Frameworkconst.TagFrameworkContext} 开始初始化框架");

                CancellationToken = cancellationToken;

                // 初始化所有Provider
                await _providerService.InitAllAsync();

                // 初始化所有Module
                await _moduleService.InitAllAsync();
                
                // 启用所有Module
                await _moduleService.EnableAllAsync();

                _isInitialized = true;
                JLogger.Log($"{Frameworkconst.TagFrameworkContext} 框架初始化完成");
            }
            catch (System.Exception ex)
            {
                JLogger.LogError($"{Frameworkconst.TagFrameworkContext} 框架初始化失败: {ex.Message}");
                throw;
            }
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

            try
            {
                JLogger.Log($"{Frameworkconst.TagFrameworkContext} 开始关闭框架");

                // 先禁用所有Module
                await _moduleService.DisableAllAsync();
                
                // 关闭所有Module
                await _moduleService.ShutdownAsync();

                // 关闭所有Provider
                await _providerService.ShutdownAllAsync();

                _isInitialized = false;
                CancellationToken = CancellationToken.None;
                JLogger.Log($"{Frameworkconst.TagFrameworkContext} 框架已关闭");
            }
            catch (System.Exception ex)
            {
                JLogger.LogError($"{Frameworkconst.TagFrameworkContext} 框架关闭失败: {ex.Message}");
                throw;
            }
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
            _container?.Clear();
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
