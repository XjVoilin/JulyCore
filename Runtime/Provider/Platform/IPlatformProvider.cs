using Cysharp.Threading.Tasks;
using JulyCore.Core;
using UnityEngine;

namespace JulyCore.Provider.Platform
{
    public interface IPlatformProvider : IProvider
    {
        int PlatformType { get; }
        T GetService<T>() where T : class;
        UniTask ColdLaunchAsync() => UniTask.CompletedTask;
        Rect GetSafeArea() => Screen.safeArea;
    }
}
