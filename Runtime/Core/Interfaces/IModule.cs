using Cysharp.Threading.Tasks;

namespace JulyCore.Core
{
    /// <summary>
    /// 游戏内的一个功能 / 系统 / 域逻辑。
    /// 外部项目可实现此接口来创建自定义模块。
    /// </summary>
    public interface IModule
    {
        string Name { get; }
        bool IsInitialized { get; }
        int Priority { get; }
        UniTask InitAsync();
        void Shutdown();
        void Update(float elapseSeconds, float realElapseSeconds);
    }
}
