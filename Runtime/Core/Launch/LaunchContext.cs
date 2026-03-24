using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Core.Config;

namespace JulyCore.Core.Launch
{
    public class LaunchContext
    {
        public FrameworkConfig Config { get; }
        public CancellationToken Token { get; }
        public IServiceRegistry Registry { get; }

        /// <summary>
        /// InitCoreProviders 完成后由框架调用，通知 JulyGameEntry 开始驱动 Update
        /// </summary>
        public Action OnCoreReady { get; set; }

        private readonly FrameworkContext _frameworkContext;
        private readonly IModuleService _moduleService;
        private readonly IProviderService _providerService;

        internal LaunchContext(
            FrameworkConfig config,
            CancellationToken token,
            IServiceRegistry registry,
            IModuleService moduleService,
            IProviderService providerService,
            FrameworkContext frameworkContext)
        {
            Config = config;
            Token = token;
            Registry = registry;
            _moduleService = moduleService;
            _providerService = providerService;
            _frameworkContext = frameworkContext;
        }

        public void RegisterModule<T>() where T : IModule, new()
            => _moduleService.RegisterModule<T>();

        public void TrackProvider(IProvider provider)
            => _providerService.Track(provider);

        public async UniTask InitProvidersAsync()
            => await _frameworkContext.InitProvidersAsync(Token);

        public async UniTask InitModulesAsync()
            => await _frameworkContext.InitModulesAsync();
    }
}
