using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Provider.UI;

namespace JulyCore.Provider.UI.Animation
{
    /// <summary>
    /// 无动画策略（默认策略）
    /// 对应UIAnimationType.None，立即完成，不播放任何动画
    /// </summary>
    internal class NoneAnimationStrategy : IUIAnimationStrategy
    {
        public UniTask PlayOpenAnimationAsync(UIInfo ui, CancellationToken cancellationToken = default)
        {
            // 无动画，立即完成
            return UniTask.CompletedTask;
        }

        public UniTask PlayCloseAnimationAsync(UIInfo ui, CancellationToken cancellationToken = default)
        {
            // 无动画，立即完成
            return UniTask.CompletedTask;
        }

        public bool IsSupported(UIBase ui)
        {
            // None策略总是支持
            return true;
        }
    }
}

