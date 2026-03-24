using Cysharp.Threading.Tasks;

namespace JulyCore.Core.Launch
{
    public interface ILaunchStep
    {
        string Name { get; }
        UniTask<bool> ExecuteAsync(LaunchContext ctx);
    }
}
