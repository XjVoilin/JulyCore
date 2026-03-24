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

            // 注册核心服务到容器
            _container.RegisterSingleton(_container);
            _container.RegisterSingleton(_moduleService);
            _container.RegisterSingleton(_providerService);
            _container.RegisterSingleton(_eventBus);
            _container.RegisterSingleton(_frameworkConfig);
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
