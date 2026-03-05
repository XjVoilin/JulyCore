using System;
using System.Collections.Generic;

namespace JulyCore.Provider.Pool
{
    /// <summary>
    /// 对象池提供者接口
    /// 提供通用对象池管理能力，支持任意类型的对象复用
    /// </summary>
    public interface IPoolProvider : Core.IProvider
    {
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
        IObjectPool<T> CreatePool<T>(
            Func<T> createFunc = null,
            Action<T> onGet = null,
            Action<T> onReturn = null,
            Action<T> onDestroy = null,
            int initialSize = 0,
            int maxSize = 0) where T : class;

        /// <summary>
        /// 获取对象池
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <returns>对象池实例，如果不存在则返回null</returns>
        IObjectPool<T> GetPool<T>() where T : class;

        /// <summary>
        /// 销毁对象池
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <returns>是否销毁成功</returns>
        bool DestroyPool<T>() where T : class;

        /// <summary>
        /// 销毁所有对象池
        /// </summary>
        void DestroyAllPools();

        /// <summary>
        /// 获取对象池统计信息
        /// </summary>
        /// <returns>统计信息字典</returns>
        Dictionary<string, object> GetPoolStatistics();
    }

    /// <summary>
    /// 对象池接口
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    public interface IObjectPool<T> where T : class
    {
        /// <summary>
        /// 从池中获取对象
        /// </summary>
        /// <returns>对象实例</returns>
        T Get();

        /// <summary>
        /// 将对象返回池中
        /// </summary>
        /// <param name="obj">对象实例</param>
        void Return(T obj);

        /// <summary>
        /// 清空池（销毁所有对象）
        /// </summary>
        void Clear();

        /// <summary>
        /// 预热池（创建指定数量的对象）
        /// </summary>
        /// <param name="count">预热数量</param>
        void Warmup(int count);

        /// <summary>
        /// 获取池中可用对象数量
        /// </summary>
        int AvailableCount { get; }

        /// <summary>
        /// 获取池中已使用对象数量
        /// </summary>
        int ActiveCount { get; }

        /// <summary>
        /// 获取池的总大小
        /// </summary>
        int TotalCount { get; }

        /// <summary>
        /// 最大个数
        /// </summary>
        int MaxSize { get; }
    }
}