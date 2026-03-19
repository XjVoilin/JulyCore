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

        protected override UniTask OnInitAsync()
        {
            _provider = GetProvider<IPlatformProvider>();
            return UniTask.CompletedTask;
        }

        public T GetService<T>() where T : class
        {
            return _provider.GetService<T>();
        }

        public UnityEngine.Rect GetSafeArea()
        {
            return _provider.GetSafeArea();
        }

        public UniTask ColdLaunchAsync()
        {
            return _provider.ColdLaunchAsync();
        }
    }
}
