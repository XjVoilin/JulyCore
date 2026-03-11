using JulyCore.Core;
using JulyCore.Provider.Base;

namespace JulyCore.Provider.Platform
{
    public class NullPlatformProvider : ProviderBase, IPlatformProvider
    {
        protected override LogChannel LogChannel => LogChannel.Platform;
        public override int Priority => Frameworkconst.PriorityPlatformProvider;

        public T GetService<T>() where T : class => null;
    }
}
