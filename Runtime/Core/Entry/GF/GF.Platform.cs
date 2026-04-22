using JulyCore.Module.Platform;
using JulyCore.Provider.Platform;

namespace JulyCore
{
    public static partial class GF
    {
        public static class Platform
        {
            private static PlatformModule _module;

            private static PlatformModule Module
            {
                get
                {
                    _module ??= GetModule<PlatformModule>();
                    return _module;
                }
            }

            public static int PlatformType => Module.PlatformType;
            public static IPlatformRoute Route => Module.Route;

            public static T GetService<T>() where T : class
            {
                return Module.GetService<T>();
            }
        }
    }
}
