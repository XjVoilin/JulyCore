using JulyCore.Module.Platform;

namespace JulyCore
{
    public static partial class GF
    {
        public static class Platform
        {
            private static PlatformModule _module;
            private static PlatformModule Module => _module ??= GetModule<PlatformModule>();

            public static int PlatformType => Module.PlatformType;
            public static T GetService<T>() where T : class => Module.GetService<T>();
            public static void DeferAllServices() => Module.DeferAllServices();
        }
    }
}
