using Cysharp.Threading.Tasks;
using JulyCore.Module.Platform;

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

            public static T GetService<T>() where T : class
            {
                return Module.GetService<T>();
            }

            public static UnityEngine.Rect GetSafeArea()
            {
                return Module.GetSafeArea();
            }

            public static UniTask ColdLaunchAsync()
            {
                return Module.ColdLaunchAsync();
            }
        }
    }
}
