using System;
using Cysharp.Threading.Tasks;
using JulyCore.Core;

namespace JulyCore
{
    public static partial class GF
    {
        /// <summary>
        /// 注册 Provider 实例并追踪生命周期。
        /// 适用于启动阶段的注册，由 InitPendingAsync / Pipeline 统一初始化。
        /// </summary>
        public static void RegisterProvider<TInterface>(TInterface instance)
            where TInterface : IProvider
        {
            if (_context == null)
                throw new InvalidOperationException(
                    $"[GF] FrameworkContext 尚未初始化，无法注册 Provider: {instance?.GetType().Name}");
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            _context.RegisterProvider(instance);
        }

        /// <summary>
        /// 注册并立即初始化 Provider 实例（运行时热替换场景）。
        /// </summary>
        public static async UniTask RegisterAndInitProvider<TInterface>(TInterface instance)
            where TInterface : IProvider
        {
            if (_context == null)
                throw new InvalidOperationException(
                    $"[GF] FrameworkContext 尚未初始化，无法注册 Provider: {instance?.GetType().Name}");
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            if (_context.Registry.TryResolve<TInterface>(out var old) && old is IProvider oldProvider)
            {
                if (oldProvider.IsInitialized) oldProvider.Shutdown();
            }

            _context.ReplaceProvider(instance);
            if (!instance.IsInitialized)
                await instance.InitAsync();
        }
    }
}
