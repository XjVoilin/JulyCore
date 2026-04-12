using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Module.Base;
using JulyCore.Provider.Pool;

namespace JulyCore.Module.Pool
{
    /// <summary>
    /// 对象池模块
    /// 纯技术层代理：Pool本身是纯技术实现（对象复用、生命周期管理）
    /// 如果未来需要业务规则（如池的创建策略、对象回收策略），可在此层添加
    /// </summary>
    internal class PoolModule : ModuleBase
    {
        private IPoolProvider _poolProvider;

        /// <summary>
        /// 日志通道
        /// </summary>
        protected override LogChannel LogChannel => LogChannel.Pool;

        /// <summary>
        /// 模块执行优先级
        /// </summary>
        public override int Priority => Frameworkconst.PriorityPoolModule;

        /// <summary>
        /// 初始化Module
        /// </summary>
        protected override UniTask OnInitAsync()
        {
            try
            {
                // 获取对象池提供者
                _poolProvider = GetProvider<IPoolProvider>();
                if (_poolProvider == null)
                {
                    throw new JulyException($"[{Name}] 未找到IPoolProvider，请先注册PoolProvider");
                }

                return base.OnInitAsync();
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 对象池模块初始化失败: {ex.Message}");
                throw;
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
        internal IObjectPool<T> CreatePool<T>(
            Func<T> createFunc = null,
            Action<T> onGet = null,
            Action<T> onReturn = null,
            Action<T> onDestroy = null,
            int initialSize = 0,
            int maxSize = 0) where T : class
        {
            return _poolProvider.CreatePool(createFunc, onGet, onReturn, onDestroy, initialSize, maxSize);
        }

        /// <summary>
        /// 回收对象
        /// </summary>
        /// <param name="obj"></param>
        /// <typeparam name="T"></typeparam>
        internal void Return<T>(T obj) where T : class
        {
            var pool = _poolProvider.GetPool<T>();
            if (pool == null)
            {
                LogWarning($"{typeof(T)}的池子不存在,不能回收");
                return;
            }
            pool.Return(obj);
        }

        /// <summary>
        /// 销毁对象池
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <returns>是否销毁成功</returns>
        internal bool DestroyPool<T>() where T : class
        {
            return _poolProvider.DestroyPool<T>();
        }

        /// <summary>
        /// 获取对象池统计信息
        /// </summary>
        /// <returns>统计信息字典</returns>
        internal Dictionary<string, object> GetPoolStatistics()
        {
            return _poolProvider.GetPoolStatistics();
        }

        /// <summary>
        /// 关闭Module
        /// </summary>
        protected override void OnShutdown()
        {
            // 销毁所有对象池
            if (_poolProvider != null)
            {
                try
                {
                    _poolProvider.DestroyAllPools();
                }
                catch (Exception ex)
                {
                    LogError($"[{Name}] 销毁对象池时异常: {ex.Message}");
                }
            }

            _poolProvider = null;
        }
    }
}

