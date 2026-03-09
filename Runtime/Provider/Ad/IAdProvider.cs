using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;

namespace JulyCore.Provider.Ad
{
    /// <summary>
    /// 广告结果
    /// </summary>
    public readonly struct AdResult
    {
        public bool IsSuccess { get; }
        public bool DidEarnReward { get; }
        public string ErrorMessage { get; }

        public AdResult(bool isSuccess, bool didEarnReward, string errorMessage = null)
        {
            IsSuccess = isSuccess;
            DidEarnReward = didEarnReward;
            ErrorMessage = errorMessage;
        }

        public static AdResult Success() => new(true, true);
        public static AdResult Fail(string error) => new(false, false, error);
    }

    /// <summary>
    /// 广告提供者接口
    /// </summary>
    public interface IAdProvider : IProvider
    {
        /// <summary>
        /// 预加载激励视频广告
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否加载成功</returns>
        UniTask<bool> LoadRewardedAdAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 展示激励视频广告
        /// 如果广告未就绪，会先尝试加载
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>广告展示结果</returns>
        UniTask<AdResult> ShowRewardedAdAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 检查激励视频广告是否已就绪
        /// </summary>
        /// <returns>是否已就绪</returns>
        bool IsRewardedAdReady();
    }
}