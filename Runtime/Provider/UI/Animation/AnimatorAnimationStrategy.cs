using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Provider.UI;
using UnityEngine;

namespace JulyCore.Provider.UI.Animation
{
    /// <summary>
    /// Animator动画策略
    /// 使用Unity Animator组件播放动画
    /// </summary>
    internal class AnimatorAnimationStrategy : IUIAnimationStrategy
    {
        public async UniTask PlayOpenAnimationAsync(UIInfo ui, CancellationToken cancellationToken = default)
        {
            if (ui == null || !ui.GameObject)
            {
                return;
            }

            var animator = ui.GameObject.GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            // 播放Open动画
            animator.Play(UIAnimationConstants.AnimatorOpenStateName);
            
            // 等待动画完成
            await WaitForAnimatorState(animator, UIAnimationConstants.AnimatorOpenStateName, cancellationToken);
        }

        public async UniTask PlayCloseAnimationAsync(UIInfo ui, CancellationToken cancellationToken = default)
        {
            if (ui == null || !ui.GameObject)
            {
                return;
            }

            var animator = ui.GameObject.GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            // 播放Close动画
            animator.Play(UIAnimationConstants.AnimatorCloseStateName);
            
            // 等待动画完成
            await WaitForAnimatorState(animator, UIAnimationConstants.AnimatorCloseStateName, cancellationToken);
        }

        public bool IsSupported(UIBase ui)
        {
            if (ui == null)
            {
                return false;
            }

            var animator = ui.GetComponent<Animator>();
            return animator != null && animator.runtimeAnimatorController != null;
        }

        /// <summary>
        /// 等待Animator状态完成
        /// </summary>
        private async UniTask WaitForAnimatorState(Animator animator, string stateName, CancellationToken cancellationToken)
        {
            if (animator == null)
            {
                return;
            }

            // 等待进入目标状态
            while (!animator.GetCurrentAnimatorStateInfo(0).IsName(stateName) && !cancellationToken.IsCancellationRequested)
            {
                await UniTask.Yield();
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            // 等待动画播放完成
            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            var animationLength = stateInfo.length;
            var normalizedTime = stateInfo.normalizedTime;

            // 如果动画已经播放完成，直接返回
            if (normalizedTime >= UIAnimationConstants.AnimatorCompleteThreshold)
            {
                return;
            }

            // 等待剩余时间
            var remainingTime = animationLength * (1f - normalizedTime);
            await UniTask.Delay((int)(remainingTime * 1000), cancellationToken: cancellationToken);
        }
    }
}

