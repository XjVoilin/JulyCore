using System;
using System.Collections.Generic;
using JulyCore.Module.Pool;
using JulyCore.Provider.Pool;

namespace JulyCore
{
    public static partial class GF
    {
        /// <summary>
        /// 对象池相关操作
        /// </summary>
        public static class Pool
        {
            private static PoolModule _module;
            private static PoolModule Module
            {
                get
                {
                    _module ??= GetModule<PoolModule>();
                    return _module;
                }
            }
            
            /// <summary>
            /// 创建对象池
            /// </summary>
            /// <typeparam name="T">对象类型</typeparam>
            /// <param name="createFunc">对象创建函数</param>
            /// <param name="onGet">从池中获取对象时的回调</param>
            /// <param name="onReturn">对象返回池时的回调</param>
            /// <param name="onDestroy">对象销毁时的回调</param>
            /// <param name="initialSize">初始大小</param>
            /// <param name="maxSize">最大大小（0表示无限制）</param>
            /// <returns>对象池实例</returns>
            /// <exception cref="InvalidOperationException">当PoolModule未注册时抛出</exception>
            public static IObjectPool<T> CreatePool<T>(
                Func<T> createFunc = null,
                Action<T> onGet = null,
                Action<T> onReturn = null,
                Action<T> onDestroy = null,
                int initialSize = 0,
                int maxSize = 0) where T : class
            {
                return Module.CreatePool(createFunc, onGet, onReturn, onDestroy, initialSize, maxSize);
            }

            /// <summary>
            /// 将对象返回对象池
            /// </summary>
            /// <typeparam name="T">对象类型</typeparam>
            /// <param name="obj">对象实例</param>
            public static void Return<T>(T obj) where T : class
            {
                if (obj == null)
                {
                    return;
                }

                Module.Return(obj);
            }

            /// <summary>
            /// 销毁对象池
            /// </summary>
            /// <typeparam name="T">对象类型</typeparam>
            /// <returns>是否销毁成功</returns>
            public static bool DestroyPool<T>() where T : class
            {
                return Module.DestroyPool<T>();
            }

            /// <summary>
            /// 获取对象池统计信息
            /// </summary>
            /// <returns>统计信息字典</returns>
            public static Dictionary<string, object> GetStatistics()
            {
                return Module.GetPoolStatistics();
            }
        }
    }
}