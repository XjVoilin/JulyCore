using System;
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
        private static FrameworkContext _context => FrameworkContext.Instance;
        
        /// <summary>
        /// 获取模块
        /// </summary>
        /// <typeparam name="T">模块类型</typeparam>
        /// <returns>模块实例，如果未注册则返回 null</returns>
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
    }
}
