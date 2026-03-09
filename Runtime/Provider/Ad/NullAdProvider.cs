using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Provider.Base;

namespace JulyCore.Provider.Ad
{
    /// <summary>
    /// 空广告提供者
    /// </summary>
    internal class NullAdProvider : ProviderBase, IAdProvider
    {
        protected override LogChannel LogChannel => LogChannel.Ad;

        public override int Priority => Frameworkconst.PriorityAdProvider;

        public UniTask<bool> LoadRewardedAdAsync(CancellationToken cancellationToken = default)
        {
            LogWarning("NullAdProvider: 未接入广告 SDK，LoadRewardedAd 返回 false");
            return UniTask.FromResult(false);
        }

        public UniTask<AdResult> ShowRewardedAdAsync(CancellationToken cancellationToken = default)
        {
            LogWarning("NullAdProvider: 未接入广告 SDK，ShowRewardedAd 返回失败");
            return UniTask.FromResult(AdResult.Fail("未接入广告 SDK"));
        }

        public bool IsRewardedAdReady()
        {
            return false;
        }
    }
}
