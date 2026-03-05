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
        private readonly object _lock = new();

        public bool IsInitialized { get; private set; }

        /// <summary>
        /// 追踪 Provider 的生命周期
        /// </summary>
        public void Track(IProvider provider)
        {
            if (provider == null) return;

            lock (_lock)
            {
                if (!_providers.Contains(provider))
                {
                    _providers.Add(provider);
                    JLogger.Log($"{Frameworkconst.TagProviderService} 追踪 Provider: {provider.GetType().Name}");
                }
            }
        }

        public async UniTask InitAllAsync()
        {
            if (IsInitialized)
            {
                JLogger.LogWarning($"{Frameworkconst.TagProviderService} Provider 已初始化，跳过");
                return;
            }

            var providers = GetProvidersSnapshot();
            JLogger.Log($"{Frameworkconst.TagProviderService} 开始初始化 {providers.Length} 个 Provider");

            foreach (var provider in providers)
            {
                if (!provider.IsInitialized)
                {
                    await provider.InitAsync();
                }
            }

            IsInitialized = true;
            JLogger.Log($"{Frameworkconst.TagProviderService} 所有 Provider 初始化完成");
        }

        public async UniTask ShutdownAllAsync()
        {
            if (!IsInitialized) return;

            var providers = GetProvidersSnapshot();
            JLogger.Log($"{Frameworkconst.TagProviderService} 开始关闭 {providers.Length} 个 Provider");

            // 逆序关闭
            for (int i = providers.Length - 1; i >= 0; i--)
            {
                var provider = providers[i];
                if (provider.IsInitialized)
                {
                    await provider.ShutdownAsync();
                }
            }

            IsInitialized = false;
            JLogger.Log($"{Frameworkconst.TagProviderService} 所有 Provider 已关闭");
        }

        public void Clear()
        {
            lock (_lock)
            {
                _providers.Clear();
            }
            IsInitialized = false;
            JLogger.Log($"{Frameworkconst.TagProviderService} 已清空所有 Provider");
        }

        private IProvider[] GetProvidersSnapshot()
        {
            lock (_lock)
            {
                // 按优先级排序
                return _providers
                    .OrderBy(p => p is IPriority priority ? priority.Priority : 0)
                    .ToArray();
            }
        }
    }
}
