using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Provider.UI;

namespace JulyCore.Provider.UI.Animation
{
    /// <summary>
    /// UI动画策略接口
    /// 使用策略模式实现不同的动画效果
    /// </summary>
    internal interface IUIAnimationStrategy
    {
        /// <summary>
        /// 播放打开动画（异步，等待动画完成）
        /// </summary>
        /// <param name="ui">UI实例</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>动画完成的任务</returns>
        UniTask PlayOpenAnimationAsync(UIInfo ui, CancellationToken cancellationToken = default);

        /// <summary>
        /// 播放关闭动画（异步，等待动画完成）
        /// </summary>
        /// <param name="ui">UI实例</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>动画完成的任务</returns>
        UniTask PlayCloseAnimationAsync(UIInfo ui, CancellationToken cancellationToken = default);

        /// <summary>
        /// 检查是否支持该动画类型
        /// </summary>
        /// <param name="ui">UI实例</param>
        /// <returns>是否支持</returns>
        bool IsSupported(UIBase ui);
    }
}

