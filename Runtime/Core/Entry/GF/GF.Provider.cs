using System;
using Cysharp.Threading.Tasks;
using JulyCore.Core;

namespace JulyCore
{
    public static partial class GF
    {
        /// <summary>
        /// 注册并初始化 Provider（供热更程序集调用）。
        /// 覆盖 DI 注册 → 解析实例 → 追踪生命周期 → 异步初始化。
        /// 必须在框架启动完成后调用。
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
                        await oldProvider.ShutdownAsync();
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
