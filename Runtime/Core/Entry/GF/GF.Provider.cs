using System;
using Cysharp.Threading.Tasks;
using JulyCore.Core;

namespace JulyCore
{
    public static partial class GF
    {
        /// <summary>
        /// 注册 Provider（仅覆盖 DI 注册，不立即初始化）。
        /// 适用于启动阶段的注册 → 由 InitPendingAsync 统一初始化。
        /// </summary>
        public static void RegisterProvider<TInterface, TImpl>()
            where TInterface : IProvider
            where TImpl : class, TInterface
        {
            if (_context == null)
                throw new InvalidOperationException(
                    $"[GF] FrameworkContext 尚未初始化，无法注册 Provider: {typeof(TImpl).Name}");

            _context.Container.RegisterSingleton<TInterface, TImpl>();
        }

        /// <summary>
        /// 注册并立即初始化 Provider（运行时热替换场景）。
        /// 覆盖 DI 注册 → 解析实例 → 追踪生命周期 → 异步初始化。
        /// </summary>
        public static async UniTask RegisterAndInitProvider<TInterface, TImpl>()
            where TInterface : IProvider
            where TImpl : class, TInterface
        {
            if (_context == null)
                throw new InvalidOperationException(
                    $"[GF] FrameworkContext 尚未初始化，无法注册 Provider: {typeof(TImpl).Name}");

            if (_context.Container.TryGetExistingSingleton<TInterface>(out var oldInstance))
            {
                if (oldInstance is IProvider oldProvider)
                {
                    _context.ProviderService.Untrack(oldProvider);
                    if (oldProvider.IsInitialized)
                        oldProvider.Shutdown();
                }
            }

            _context.Container.RegisterSingleton<TInterface, TImpl>();
            if (_context.Container.Resolve<TInterface>() is IProvider provider)
            {
                _context.ProviderService.Track(provider);
                if (!provider.IsInitialized)
                    await provider.InitAsync();
            }
        }
    }
}
