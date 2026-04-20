using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Module.Base;
using JulyCore.Provider.Platform;

namespace JulyCore.Module.Platform
{
    internal class PlatformModule : ModuleBase
    {
        private IPlatformProvider _provider;

        protected override LogChannel LogChannel => LogChannel.Platform;
        public override int Priority => Frameworkconst.PriorityPlatformModule;

        public int PlatformType => _provider.PlatformType;

        protected override UniTask OnInitAsync()
        {
            _provider = GetProvider<IPlatformProvider>();
            return UniTask.CompletedTask;
        }

        public T GetService<T>() where T : class
        {
            return _provider.GetService<T>();
        }
    }
}
