using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using JulyCore.Data.UI;
using UnityEngine;

namespace JulyCore.Provider.UI.Animation
{
    /// <summary>
    /// 滑动动画策略
    /// 支持从上下左右四个方向滑入滑出
    /// 使用 DOTween 实现平滑动画
    /// </summary>
    internal class SlideAnimationStrategy : IUIAnimationStrategy
    {
        private readonly UIAnimationType _slideDirection;

        public SlideAnimationStrategy(UIAnimationType slideDirection)
        {
            _slideDirection = slideDirection;
        }

        public async UniTask PlayOpenAnimationAsync(UIInfo ui, CancellationToken cancellationToken = default)
        {
            if (ui == null || !ui.GameObject)
            {
                return;
            }

            var rectTransform = ui.GameObject.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                return;
            }

            // 先清理之前可能存在的tween，避免动画冲突
            rectTransform.DOKill();

            var targetPosition = rectTransform.anchoredPosition;
            var startPosition = GetStartPosition(rectTransform, targetPosition);

            // 设置初始位置
            rectTransform.anchoredPosition = startPosition;

            // 使用 DOTween 滑动动画
            // SetAutoKill(true)：动画完成后自动清理，防止内存泄漏
            var tween = rectTransform
                .DOAnchorPos(targetPosition, UIAnimationConstants.DefaultDuration)
                .SetEase(Ease.OutCubic)
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

            var rectTransform = ui.GameObject.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                return;
            }

            // 先清理之前可能存在的tween，避免动画冲突
            rectTransform.DOKill();

            var currentPosition = rectTransform.anchoredPosition;
            var targetPosition = GetStartPosition(rectTransform, currentPosition);

            // 使用 DOTween 滑动动画
            // SetAutoKill(true)：动画完成后自动清理，防止内存泄漏
            var tween = rectTransform
                .DOAnchorPos(targetPosition, UIAnimationConstants.DefaultDuration)
                .SetEase(Ease.InCubic)
                .SetUpdate(true)
                .SetAutoKill(true);

            // 等待动画完成，支持取消
            await tween.ToUniTask(cancellationToken: cancellationToken);
        }

        public bool IsSupported(UIBase ui)
        {
            return ui != null && ui.GetComponent<RectTransform>() != null;
        }

        private Vector2 GetStartPosition(RectTransform rectTransform, Vector2 currentPosition)
        {
            var screenSize = new Vector2(Screen.width, Screen.height);
            var canvas = rectTransform.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                screenSize = new Vector2(Screen.width, Screen.height);
            }
            else if (canvas != null)
            {
                var canvasRect = canvas.GetComponent<RectTransform>();
                if (canvasRect != null)
                {
                    screenSize = canvasRect.sizeDelta;
                }
            }

            switch (_slideDirection)
            {
                case UIAnimationType.SlideFromTop:
                    return new Vector2(currentPosition.x, screenSize.y);
                case UIAnimationType.SlideFromBottom:
                    return new Vector2(currentPosition.x, -screenSize.y);
                case UIAnimationType.SlideFromLeft:
                    return new Vector2(-screenSize.x, currentPosition.y);
                case UIAnimationType.SlideFromRight:
                    return new Vector2(screenSize.x, currentPosition.y);
                default:
                    return currentPosition;
            }
        }
    }
}

