using JulyCore.Core;
using JulyCore.Module.Base;

namespace JulyCore
{
    /// <summary>
    /// 框架公共API门面类
    /// 提供简洁的上层调用接口，用于上层项目代码调用框架功能
    /// </summary>
    public static partial class GF
    {
        private static CoreContext _context => CoreContext.Instance;
        
        /// <summary>
        /// 获取模块
        /// </summary>
        private static T GetModule<T>() where T : IModule
        {
            var module = _context.ModuleService.GetModule<T>();
            if (module == null)
            {
                var t = typeof(T);
                JLogger.LogError($"{t}未注册,需要先注册");
            }

            return module;
        }


        /// <summary>
        /// 基础设施事件总线（Scene/Network/Audio 等）
        /// </summary>
        public static JulyEvents.IEventBus CoreEvent => _context.EventBus;

        /// <summary>
        /// 从服务注册表解析已注册的服务实例
        /// </summary>
        public static T Resolve<T>() => _context.Registry.Resolve<T>();

        /// <summary>
        /// 尝试从服务注册表解析已注册的服务实例
        /// </summary>
        public static bool TryResolve<T>(out T instance) => _context.Registry.TryResolve(out instance);
    }
}
