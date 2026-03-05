using System;
using System.Collections.Generic;
using JulyCore.Provider.UI;
using UnityEngine;

namespace JulyCore.Provider.UI.Pool
{
    /// <summary>
    /// UI对象池
    /// 管理UI实例的复用，减少频繁创建和销毁带来的性能开销
    /// </summary>
    internal class UIPool
    {
        private readonly Dictionary<Type, Queue<UIBase>> _instancePool = new Dictionary<Type, Queue<UIBase>>();
        private readonly Dictionary<Type, GameObject> _prefabCache = new Dictionary<Type, GameObject>();
        private readonly int _maxSizePerType;

        public UIPool(int maxSizePerType = 5)
        {
            _maxSizePerType = maxSizePerType;
        }

        /// <summary>
        /// 注册预制体到对象池
        /// </summary>
        /// <param name="uiType">UI类型</param>
        /// <param name="prefab">预制体</param>
        public void RegisterPrefab(Type uiType, GameObject prefab)
        {
            if (prefab == null)
            {
                return;
            }

            _prefabCache[uiType] = prefab;
        }

        /// <summary>
        /// 从对象池获取或创建UI实例
        /// </summary>
        /// <param name="uiType">UI类型</param>
        /// <param name="parent">父节点</param>
        /// <returns>UI实例，如果无法创建则返回null</returns>
        public UIBase GetOrCreate(Type uiType, Transform parent)
        {
            if (uiType == null)
            {
                return null;
            }
            
            // 尝试从池中获取
            if (_instancePool.TryGetValue(uiType, out var instanceQueue) && instanceQueue.Count > 0)
            {
                var instance = instanceQueue.Dequeue();
                if (instance != null)
                {
                    instance.gameObject.transform.SetParent(parent);
                    instance.gameObject.SetActive(true);
                    return instance;
                }
            }

            // 池中没有或实例已被销毁，创建新实例
            if (_prefabCache.TryGetValue(uiType, out var prefab))
            {
                var gameObject = UnityEngine.Object.Instantiate(prefab, parent);
                gameObject.transform.SetParent(parent, false);
                var ui = gameObject.GetComponent(uiType) as UIBase;
                return ui;
            }

            return null;
        }

        /// <summary>
        /// 将UI实例回收到对象池
        /// </summary>
        /// <param name="uiType">UI类型</param>
        /// <param name="ui">UI实例</param>
        public void ReturnToPool(Type uiType, UIBase ui)
        {
            if (ui == null)
            {
                return;
            }

            // 调用UI的OnClose方法重置状态
            try
            {
                ui.Close();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UIPool] 调用UI OnClose失败: {ex.Message}");
            }

            // 确保该类型的队列存在
            if (!_instancePool.TryGetValue(uiType, out var instanceQueue))
            {
                instanceQueue = new Queue<UIBase>();
                _instancePool[uiType] = instanceQueue;
            }

            // 限制池大小，避免内存占用过大
            if (instanceQueue.Count >= _maxSizePerType)
            {
                Debug.Log($"超过了池子的大小:{_maxSizePerType}  {ui.gameObject.name}");
                UnityEngine.Object.Destroy(ui.gameObject);
                return;
            }

            // 重置实例状态并回收到池中
            ui.gameObject.SetActive(false);
            ui.gameObject.transform.SetParent(null);
            instanceQueue.Enqueue(ui);
        }

        /// <summary>
        /// 清空指定类型的对象池
        /// </summary>
        /// <param name="uiType">UI类型</param>
        public void ClearPool(Type uiType)
        {
            if (_instancePool.TryGetValue(uiType, out var instanceQueue))
            {
                while (instanceQueue.Count > 0)
                {
                    var instance = instanceQueue.Dequeue();
                    if (instance != null)
                    {
                        UnityEngine.Object.Destroy(instance.gameObject);
                    }
                }
                _instancePool.Remove(uiType);
            }
        }

        /// <summary>
        /// 清空所有对象池
        /// </summary>
        public void ClearAllPools()
        {
            foreach (var instanceQueue in _instancePool.Values)
            {
                while (instanceQueue.Count > 0)
                {
                    var instance = instanceQueue.Dequeue();
                    if (instance != null)
                    {
                        UnityEngine.Object.Destroy(instance.gameObject);
                    }
                }
            }
            _instancePool.Clear();
            _prefabCache.Clear();
        }
    }
}

