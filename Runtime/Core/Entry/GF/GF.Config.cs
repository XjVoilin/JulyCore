using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Module.Config;
using JulyCore.Provider.Config;

namespace JulyCore
{
    public static partial class GF
    {
        /// <summary>
        /// 配置模块入口
        /// </summary>
        public static class Config
        {
            private static ConfigModule _module;
            private static ConfigModule Module => _module ??= GetModule<ConfigModule>();

            /// <summary>
            /// 获取配置表
            /// </summary>
            /// <typeparam name="T">配置表类型（IConfigTable 或 Luban Tables 等）</typeparam>
            public static T GetTable<T>() where T : class
            {
                var isSuccess = Module.TryGetTable<T>(out var table);
                if (isSuccess)
                {
                    return table;
                }
                
                JLogger.LogError($"配置表:{typeof(T).Name}未找到");
                return null;
            }
        }
    }
}
