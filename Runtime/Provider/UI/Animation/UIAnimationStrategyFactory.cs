using System.Collections.Generic;
using JulyCore.Data.UI;
using UnityEngine;

namespace JulyCore.Provider.UI.Animation
{
    /// <summary>
    /// UI动画策略工厂
    /// 根据动画类型创建对应的策略实例
    /// </summary>
    internal static class UIAnimationStrategyFactory
    {
        private static readonly Dictionary<UIAnimationType, IUIAnimationStrategy> _strategyCache = new Dictionary<UIAnimationType, IUIAnimationStrategy>();

        /// <summary>
        /// 创建动画策略
        /// </summary>
        /// <param name="animationType">动画类型</param>
        /// <returns>动画策略实例</returns>
        public static IUIAnimationStrategy CreateStrategy(UIAnimationType animationType)
        {
            // None类型使用默认无动画策略
            if (animationType == UIAnimationType.None)
            {
                if (!_strategyCache.TryGetValue(UIAnimationType.None, out var noneStrategy))
                {
                    noneStrategy = new NoneAnimationStrategy();
                    _strategyCache[UIAnimationType.None] = noneStrategy;
                }
                return noneStrategy;
            }

            // 滑动动画需要根据具体方向创建（不使用缓存，因为每个方向都是不同的实例）
            if (IsSlideAnimation(animationType))
            {
                return new SlideAnimationStrategy(animationType);
            }

            // 其他类型使用缓存
            if (!_strategyCache.TryGetValue(animationType, out var strategy))
            {
                strategy = CreateStrategyInternal(animationType);
                if (strategy != null)
                {
                    _strategyCache[animationType] = strategy;
                }
            }

            return strategy;
        }

        /// <summary>
        /// 创建策略实例（内部方法）
        /// </summary>
        private static IUIAnimationStrategy CreateStrategyInternal(UIAnimationType animationType)
        {
            return animationType switch
            {
                UIAnimationType.None => new NoneAnimationStrategy(),
                UIAnimationType.Animator => new AnimatorAnimationStrategy(),
                UIAnimationType.Fade => new FadeAnimationStrategy(),
                UIAnimationType.Scale => new ScaleAnimationStrategy(),
                _ => null
            };
        }

        /// <summary>
        /// 检查是否为滑动动画
        /// </summary>
        private static bool IsSlideAnimation(UIAnimationType animationType)
        {
            return animationType == UIAnimationType.SlideFromTop ||
                   animationType == UIAnimationType.SlideFromBottom ||
                   animationType == UIAnimationType.SlideFromLeft ||
                   animationType == UIAnimationType.SlideFromRight;
        }
    }
}

