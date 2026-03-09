using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Module.Ad;
using JulyCore.Provider.Ad;

namespace JulyCore
{
    public static partial class GF
    {
        /// <summary>
        /// 广告相关操作
        /// 只有激励视频
        /// </summary>
        public static class Ad
        {
            private static AdModule _module;

            private static AdModule Module
            {
                get
                {
                    _module ??= GetModule<AdModule>();
                    return _module;
                }
            }

            /// <summary>
            /// 预加载激励视频广告
            /// </summary>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>是否加载成功</returns>
            public static UniTask<bool> LoadRewardedAdAsync(CancellationToken cancellationToken = default)
            {
                return Module.LoadRewardedAdAsync(cancellationToken);
            }

            /// <summary>
            /// 展示激励视频广告
            /// </summary>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>广告展示结果</returns>
            public static UniTask<AdResult> ShowRewardedAdAsync(CancellationToken cancellationToken = default)
            {
                return Module.ShowRewardedAdAsync(cancellationToken);
            }

            /// <summary>
            /// 检查激励视频广告是否已就绪
            /// </summary>
            /// <returns>是否已就绪</returns>
            public static bool IsRewardedAdReady()
            {
                return Module.IsRewardedAdReady();
            }
        }
    }
}
