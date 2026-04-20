using JulyCore.Core;
using UnityEngine;

namespace JulyCore.Provider.Platform
{
    public interface IPlatformProvider : IProvider
    {
        int PlatformType { get; }
        T GetService<T>() where T : class;
        Rect GetSafeArea() => Screen.safeArea;
    }
}
