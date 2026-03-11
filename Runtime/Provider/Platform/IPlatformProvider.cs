using Cysharp.Threading.Tasks;
using JulyCore.Core;

namespace JulyCore.Provider.Platform
{
    public interface IPlatformProvider : IProvider
    {
        T GetService<T>() where T : class;
        UniTask ColdLaunchAsync() => UniTask.CompletedTask;
    }
}
