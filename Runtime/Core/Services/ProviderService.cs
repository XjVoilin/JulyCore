using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace JulyCore.Core
{
    /// <summary>
    /// Provider 生命周期管理器
    /// 仅负责 Init/Shutdown 的统一调度，不负责服务定位
    /// Provider 的注册和获取通过 IDependencyContainer 统一处理
    /// </summary>
    internal class ProviderService : IProviderService
    {
        private readonly List<IProvider> _providers = new();

        public bool IsInitialized { get; private set; }

        /// <summary>
        /// 追踪 Provider 的生命周期
        /// </summary>
        public void Track(IProvider provider)
        {
            if (provider == null) return;

            if (!_providers.Contains(provider))
            {
                _providers.Add(provider);
            }
        }

        /// <summary>
        /// 移除对 Provider 的生命周期追踪
        /// </summary>
        public void Untrack(IProvider provider)
        {
            if (provider == null) return;
            _providers.Remove(provider);
        }

        public async UniTask InitAllAsync()
        {
            var providers = GetProvidersSnapshot();
            var newCount = 0;

            foreach (var provider in providers)
            {
                if (!provider.IsInitialized)
                {
                    await provider.InitAsync();
                    newCount++;
                }
            }

            IsInitialized = true;

            if (newCount > 0)
                JLogger.Log($"{Frameworkconst.TagProviderService} {newCount} 个 Provider 初始化完成");
        }

        public void ShutdownAll()
        {
            if (!IsInitialized) return;

            var providers = GetProvidersSnapshot();

            for (int i = providers.Length - 1; i >= 0; i--)
            {
                var provider = providers[i];
                if (provider.IsInitialized)
                {
                    try
                    {
                        provider.Shutdown();
                    }
                    catch (Exception ex)
                    {
                        JLogger.LogException(ex);
                    }
                }
            }

            IsInitialized = false;
            JLogger.Log($"{Frameworkconst.TagProviderService} {providers.Length} 个 Provider 已关闭");
        }

        public void Clear()
        {
            _providers.Clear();
            IsInitialized = false;
        }

        private IProvider[] GetProvidersSnapshot()
        {
            return _providers
                .OrderBy(p => p is IPriority priority ? priority.Priority : 0)
                .ToArray();
        }
    }
}
