using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using JulyCore.Provider.UI;
using UnityEngine;

namespace JulyCore.Provider.UI.Animation
{
    /// <summary>
    /// 缩放动画策略
    /// 使用 DoTween 实现缩放动画效果
    /// </summary>
    internal class ScaleAnimationStrategy : IUIAnimationStrategy
    {
        public async UniTask PlayOpenAnimationAsync(UIInfo ui, CancellationToken cancellationToken = default)
        {
            if (ui == null || !ui.GameObject)
            {
                return;
            }

            var transform = ui.GameObject.transform;
            
            // 为什么需要DOKill()而不是只依赖AutoKill？
            // 1. AutoKill只在tween完成时生效，如果用户在动画进行中快速打开/关闭窗口，
            //    旧的tween还在运行，新tween就已经创建，两个tween会同时作用于同一个transform，导致冲突
            // 2. DOKill()可以立即停止并清理所有旧tween，确保新动画从正确的初始状态开始
            // 注意：这里不传complete参数（默认为false），因为我们会在kill后立即设置新的初始状态
            transform.DOKill();
            
            // 重置scale为初始状态（确保每次打开动画都从相同状态开始）
            transform.localScale = UIAnimationConstants.ScaleIn;

            // 使用 DoTween 实现末尾回弹效果（Ease.OutBack）
            // SetAutoKill(true)：动画完成后自动清理，防止内存泄漏（DoTween默认行为，显式设置更清晰）
            var tween = transform.DOScale(Vector3.one, UIAnimationConstants.DefaultDuration)
                .SetEase(Ease.OutBack, 3f)
                .SetUpdate(true)
                .SetAutoKill(true);

            // 等待动画完成，支持取消
            await tween.ToUniTask(cancellationToken: cancellationToken);
        }

        public async UniTask PlayCloseAnimationAsync(UIInfo ui, CancellationToken cancellationToken = default)
        {
            if (ui == null || !ui.GameObject)
            {
                return;
            }

            var transform = ui.GameObject.transform;
            
            transform.DOKill();
            
            transform.localScale = Vector3.one;
            
            var tween = transform.DOScale(UIAnimationConstants.ScaleOut, UIAnimationConstants.DefaultDuration)
                .SetEase(Ease.InBack, 0.5f)
                .SetUpdate(true)
                .SetAutoKill(true);

            // 等待动画完成，支持取消
            await tween.ToUniTask(cancellationToken: cancellationToken);
        }

        public bool IsSupported(UIBase ui)
        {
            // 缩放动画总是支持的
            return ui != null;
        }

    }
}