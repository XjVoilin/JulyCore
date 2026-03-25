using Cysharp.Threading.Tasks;
using JulyCore.Core.Launch;
using JulyCore.Module.Data;
using JulyCore.Module.Fsm;
using JulyCore.Module.Http;
using JulyCore.Module.Performance;
using JulyCore.Module.Platform;
using JulyCore.Module.Pool;
using JulyCore.Module.Resource;
using JulyCore.Module.Scene;
using JulyCore.Module.Time;

namespace JulyCore.Core.Launch.Steps
{
    public class RegisterCoreModulesStep : ILaunchStep
    {
        public string Name => "Register Core Modules";

        public UniTask<bool> ExecuteAsync(LaunchContext ctx)
        {
            ctx.RegisterModule<ResourceModule>();
            ctx.RegisterModule<TimeModule>();
            ctx.RegisterModule<SerializeModule>();
            ctx.RegisterModule<FsmModule>();
            ctx.RegisterModule<PoolModule>();
            ctx.RegisterModule<PerformanceModule>();
            ctx.RegisterModule<SceneModule>();
            ctx.RegisterModule<PlatformModule>();
            ctx.RegisterModule<HttpModule>();
            return UniTask.FromResult(true);
        }
    }
}
