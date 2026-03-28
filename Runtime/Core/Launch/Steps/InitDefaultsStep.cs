using Cysharp.Threading.Tasks;
using JulyCore.Core.Launch;

namespace JulyCore.Core.Launch.Steps
{
    public class InitDefaultsStep : ILaunchStep
    {
        public string Name => "Init Defaults";

        public async UniTask<bool> ExecuteAsync(LaunchContext ctx)
        {
            await ctx.InitProvidersAsync();
            await ctx.InitModulesAsync();
            ctx.OnCoreReady?.Invoke();
            return true;
        }
    }
}
