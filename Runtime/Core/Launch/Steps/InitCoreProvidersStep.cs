using Cysharp.Threading.Tasks;
using JulyCore.Core.Launch;

namespace JulyCore.Core.Launch.Steps
{
    public class InitCoreProvidersStep : ILaunchStep
    {
        public string Name => "Init Core Providers";

        public async UniTask<bool> ExecuteAsync(LaunchContext ctx)
        {
            await ctx.InitProvidersAsync();
            await ctx.InitModulesAsync();

            ctx.OnCoreReady?.Invoke();

            return true;
        }
    }
}
