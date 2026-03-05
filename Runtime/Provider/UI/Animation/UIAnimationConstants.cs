using UnityEngine;

namespace JulyCore.Provider.UI.Animation
{
    /// <summary>
    /// UI动画相关常量
    /// 集中管理动画策略中使用的固定值
    /// </summary>
    internal static class UIAnimationConstants
    {
        /// <summary>
        /// 默认动画持续时间（秒）
        /// </summary>
        public const float DefaultDuration = 0.3f;

        /// <summary>
        /// Animator打开动画状态名称
        /// </summary>
        public const string AnimatorOpenStateName = "Open";

        /// <summary>
        /// Animator关闭动画状态名称
        /// </summary>
        public const string AnimatorCloseStateName = "Close";

        /// <summary>
        /// 缓动函数指数（用于ease out效果）
        /// </summary>
        public const float EasingExponent = 3f;

        /// <summary>
        /// 淡入淡出动画的起始Alpha值（完全透明）
        /// </summary>
        public const float FadeStartAlpha = 0f;

        /// <summary>
        /// 淡入淡出动画的结束Alpha值（完全不透明）
        /// </summary>
        public const float FadeEndAlpha = 1f;

        /// <summary>
        /// 打开缩放动画的起始缩放值
        /// </summary>
        public static readonly Vector3 ScaleIn = Vector3.one * 0.7f;

        /// <summary>
        /// 关闭缩放动画的结束缩放值
        /// </summary>
        public static readonly Vector3 ScaleOut = Vector3.one * 0.5f;

        /// <summary>
        /// Animator动画完成判断阈值（normalizedTime >= 1f 表示动画完成）
        /// </summary>
        public const float AnimatorCompleteThreshold = 1f;
    }
}

