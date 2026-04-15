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

        internal LaunchContext(
            FrameworkConfig config,
            CancellationToken token,
            FrameworkContext frameworkContext)
        {
            Config = config;
            Token = token;
            Registry = frameworkContext.Registry;
            _moduleService = frameworkContext.ModuleService;
            _frameworkContext = frameworkContext;
        }

        public void RegisterModule<T>() where T : IModule, new()
            => _moduleService.RegisterModule<T>();

        public void RegisterProvider<T>(T provider) where T : IProvider
            => _frameworkContext.RegisterProvider(provider);

        public void ReplaceProvider<T>(T newProvider) where T : IProvider
            => _frameworkContext.ReplaceProvider(newProvider);

        public async UniTask InitProvidersAsync()
            => await _frameworkContext.InitProvidersAsync();

        public async UniTask InitModulesAsync()
            => await _frameworkContext.InitModulesAsync();
    }
}
