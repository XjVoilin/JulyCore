using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;

namespace JulyCore.Provider.UI.Animation
{
    /// <summary>
    /// 淡入淡出动画策略
    /// 使用 DoTween 实现 CanvasGroup 的 alpha 淡入淡出效果
    /// </summary>
    internal class FadeAnimationStrategy : IUIAnimationStrategy
    {
        public async UniTask PlayOpenAnimationAsync(UIInfo ui, CancellationToken cancellationToken = default)
        {
            if (ui == null)
            {
                return;
            }

            var canvasGroup = ui.CanvasGroup;
            if (canvasGroup == null)
            {
                return;
            }

            // 先清理之前可能存在的tween，避免动画冲突
            canvasGroup.DOKill();
            
            // 设置初始alpha值（交互状态由UIProvider统一管理，不在这里设置）
            canvasGroup.alpha = UIAnimationConstants.FadeStartAlpha;

            // 使用 DoTween 实现淡入动画
            // SetAutoKill(true)：动画完成后自动清理，防止内存泄漏
            var tween = canvasGroup.DOFade(UIAnimationConstants.FadeEndAlpha, UIAnimationConstants.DefaultDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetAutoKill(true);

            // 等待动画完成，支持取消
            await tween.ToUniTask(cancellationToken: cancellationToken);
        }

        public async UniTask PlayCloseAnimationAsync(UIInfo ui, CancellationToken cancellationToken = default)
        {
            if (ui == null)
            {
                return;
            }

            var canvasGroup = ui.CanvasGroup;
            if (canvasGroup == null)
            {
                return;
            }

            // 先清理之前可能存在的tween，避免动画冲突
            canvasGroup.DOKill();

            // 使用 DoTween 实现淡出动画
            // SetAutoKill(true)：动画完成后自动清理，防止内存泄漏
            // 注意：交互状态由UIProvider统一管理，不在这里设置
            var tween = canvasGroup.DOFade(UIAnimationConstants.FadeStartAlpha, UIAnimationConstants.DefaultDuration)
                .SetEase(Ease.InQuad)
                .SetUpdate(true)
                .SetAutoKill(true);

            // 等待动画完成，支持取消
            await tween.ToUniTask(cancellationToken: cancellationToken);
        }

        public bool IsSupported(UIBase ui)
        {
            // 淡入淡出动画总是支持的（会自动添加CanvasGroup）
            return ui != null;
        }
    }
}

