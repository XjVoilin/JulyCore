using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Module.Base;
using JulyCore.Provider.Ad;

namespace JulyCore.Module.Ad
{
    /// <summary>
    /// 广告模块
    /// </summary>
    internal class AdModule : ModuleBase
    {
        private IAdProvider _adProvider;

        protected override LogChannel LogChannel => LogChannel.Ad;

        public override int Priority => Frameworkconst.PriorityAdModule;

        /// <summary>
        /// 展示完成后是否自动预加载下一条
        /// </summary>
        private bool _autoPreload = true;

        protected override UniTask OnInitAsync()
        {
            try
            {
                _adProvider = GetProvider<IAdProvider>();
                return base.OnInitAsync();
            }
            catch (Exception ex)
            {
                LogError($"广告模块初始化失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 预加载激励视频广告
        /// </summary>
        internal UniTask<bool> LoadRewardedAdAsync(CancellationToken cancellationToken = default)
        {
            EnsureProvider();
            return _adProvider.LoadRewardedAdAsync(cancellationToken);
        }

        /// <summary>
        /// 展示激励视频广告
        /// 展示完成后自动预加载下一条（如果启用）
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        internal async UniTask<AdResult> ShowRewardedAdAsync(
            CancellationToken cancellationToken = default)
        {
            EnsureProvider();

            var result = await _adProvider.ShowRewardedAdAsync(cancellationToken);

            if (_autoPreload)
            {
                _adProvider.LoadRewardedAdAsync(GFCancellationToken).Forget();
            }

            return result;
        }

        /// <summary>
        /// 检查激励视频广告是否已就绪
        /// </summary>
        internal bool IsRewardedAdReady()
        {
            EnsureProvider();
            return _adProvider.IsRewardedAdReady();
        }

        private void EnsureProvider()
        {
            if (_adProvider == null)
            {
                throw new InvalidOperationException($"[{Name}] AdProvider 未初始化");
            }
        }

        protected override async UniTask OnShutdownAsync()
        {
            _adProvider = null;
            await base.OnShutdownAsync();
        }
    }
}