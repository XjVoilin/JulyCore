using Cysharp.Threading.Tasks;

namespace JulyCore.Provider.Platform
{
    public interface IPlatformService
    {
        void Init() { }
        void PostInit() { }
        UniTask PostInitAsync() => UniTask.CompletedTask;
        void DeferredInit() { }
    }
}
