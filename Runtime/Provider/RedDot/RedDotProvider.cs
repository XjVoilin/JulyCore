using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Data.RedDot;
using JulyCore.Provider.Base;

namespace JulyCore.Provider.RedDot
{
    /// <summary>
    /// 红点存储提供者实现
    /// 纯技术层：仅负责红点树结构存储、节点CRUD操作
    /// 
    /// 【设计说明】
    /// - 树结构管理（父子关系、递归计算）是数据存储的内部实现细节
    /// - 全局启用开关是数据查询的过滤条件，不是业务规则
    /// - 这些逻辑放在 Provider 中是为了保证数据一致性和查询性能
    /// </summary>
    internal class RedDotProvider : ProviderBase, IRedDotProvider
    {
        public override int Priority => Frameworkconst.PriorityRedDotProvider;
        protected override LogChannel LogChannel => LogChannel.RedDot;

        // 节点存储：Key -> Node
        private readonly Dictionary<string, RedDotNode> _storage = new();
        private readonly object _lock = new();
        
        // 全局启用开关（数据查询过滤条件，O(1) 检查）
        private bool _globalEnabled = true;

        protected override UniTask OnInitAsync()
        {
            Log($"[{Name}] 红点存储提供者初始化完成");
            return UniTask.CompletedTask;
        }

        protected override UniTask OnShutdownAsync()
        {
            lock (_lock)
            {
                _storage.Clear();
            }
            Log($"[{Name}] 红点存储提供者已关闭");
            return UniTask.CompletedTask;
        }

        #region 节点存储（CRUD）

        public bool Store(string key, string parentKey = null, RedDotType type = RedDotType.Normal)
        {
            if (string.IsNullOrEmpty(key))
            {
                LogWarning($"[{Name}] 存储失败：节点Key为空");
                return false;
            }

            lock (_lock)
            {
                if (_storage.ContainsKey(key))
                {
                    LogWarning($"[{Name}] 节点 {key} 已存在");
                    return false;
                }

                // 检查父节点是否存在
                if (!string.IsNullOrEmpty(parentKey) && !_storage.ContainsKey(parentKey))
                {
                    LogWarning($"[{Name}] 存储失败：父节点 {parentKey} 不存在");
                    return false;
                }

                var node = new RedDotNode
                {
                    Key = key,
                    Type = type,
                    ParentKey = parentKey,
                    ChildKeys = new List<string>(),
                    Count = 0,
                    CachedTotalCount = 0,
                    IsCacheValid = true
                };

                _storage[key] = node;

                // 添加到父节点的子节点列表
                if (!string.IsNullOrEmpty(parentKey) && _storage.TryGetValue(parentKey, out var parentNode))
                {
                    parentNode.ChildKeys.Add(key);
                    InvalidateCacheUpward(parentKey);
                }

                return true;
            }
        }

        public void StoreBatch(IEnumerable<(string Key, string ParentKey, RedDotType Type)> nodes)
        {
            if (nodes == null) return;

            // 先注册根节点，再注册子节点
            var nodeList = nodes.ToList();
            var rootNodes = nodeList.Where(n => string.IsNullOrEmpty(n.ParentKey)).ToList();
            var childNodes = nodeList.Where(n => !string.IsNullOrEmpty(n.ParentKey)).ToList();

            foreach (var node in rootNodes)
            {
                Store(node.Key, node.ParentKey, node.Type);
            }

            // 按深度排序注册子节点
            var maxIterations = 100;
            var iteration = 0;
            while (childNodes.Count > 0 && iteration < maxIterations)
            {
                var registered = new List<(string Key, string ParentKey, RedDotType Type)>();
                foreach (var node in childNodes)
                {
                    if (Exists(node.ParentKey))
                    {
                        Store(node.Key, node.ParentKey, node.Type);
                        registered.Add(node);
                    }
                }

                foreach (var node in registered)
                {
                    childNodes.Remove(node);
                }

                iteration++;
            }
        }

        public bool Remove(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;

            lock (_lock)
            {
                if (!_storage.TryGetValue(key, out var node))
                    return false;

                // 递归删除所有子节点
                if (node.ChildKeys != null)
                {
                    var childKeysCopy = node.ChildKeys.ToList();
                    foreach (var childKey in childKeysCopy)
                    {
                        Remove(childKey);
                    }
                }

                // 从父节点的子节点列表中移除
                if (!string.IsNullOrEmpty(node.ParentKey) && _storage.TryGetValue(node.ParentKey, out var parentNode))
                {
                    parentNode.ChildKeys.Remove(key);
                    InvalidateCacheUpward(node.ParentKey);
                }

                _storage.Remove(key);
                return true;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _storage.Clear();
            }
        }

        #endregion

        #region 节点查询

        public RedDotNode Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            lock (_lock)
            {
                return _storage.GetValueOrDefault(key);
            }
        }

        public bool Exists(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;

            lock (_lock)
            {
                return _storage.ContainsKey(key);
            }
        }

        public List<RedDotNode> GetAll()
        {
            lock (_lock)
            {
                return _storage.Values.ToList();
            }
        }

        public List<RedDotNode> GetRootNodes()
        {
            lock (_lock)
            {
                return _storage.Values.Where(n => string.IsNullOrEmpty(n.ParentKey)).ToList();
            }
        }

        public List<RedDotNode> GetChildren(string key)
        {
            lock (_lock)
            {
                if (!_storage.TryGetValue(key, out var node))
                    return new List<RedDotNode>();

                var children = new List<RedDotNode>();
                foreach (var childKey in node.ChildKeys)
                {
                    if (_storage.TryGetValue(childKey, out var childNode))
                        children.Add(childNode);
                }
                return children;
            }
        }

        public List<RedDotNode> GetLeafNodes()
        {
            lock (_lock)
            {
                return _storage.Values.Where(n => n.IsLeaf).ToList();
            }
        }

        public List<string> GetPath(string key)
        {
            var path = new List<string>();

            lock (_lock)
            {
                var currentKey = key;
                while (!string.IsNullOrEmpty(currentKey) && _storage.TryGetValue(currentKey, out var node))
                {
                    path.Insert(0, currentKey);
                    currentKey = node.ParentKey;
                }
            }

            return path;
        }

        #endregion

        #region 红点值操作

        public List<RedDotChangeInfo> SetCount(string key, int count)
        {
            var changes = new List<RedDotChangeInfo>();

            lock (_lock)
            {
                if (!_storage.TryGetValue(key, out var node))
                    return changes;

                // 检查是否为叶子节点
                if (!node.IsLeaf)
                {
                    LogWarning($"[{Name}] SetCount 应只对叶子节点调用，'{key}' 是非叶子节点，操作已忽略");
                    return changes;
                }

                count = Math.Max(0, count);
                var oldCount = node.Count;

                if (oldCount == count)
                    return changes;

                node.Count = count;
                node.IsCacheValid = false;

                changes.Add(new RedDotChangeInfo
                {
                    Key = key,
                    OldCount = oldCount,
                    NewCount = count,
                    Type = node.Type
                });

                // 向上更新父节点
                var parentChanges = UpdateParentCounts(node.ParentKey);
                changes.AddRange(parentChanges);
            }

            return changes;
        }

        public List<RedDotChangeInfo> SetCountBatch(Dictionary<string, int> counts)
        {
            if (counts == null || counts.Count == 0)
                return new List<RedDotChangeInfo>();

            // 使用字典记录每个节点的变更，自动去重（只保留最终状态）
            var changeDict = new Dictionary<string, RedDotChangeInfo>();

            lock (_lock)
            {
                // 1. 记录所有节点的旧值，批量设置新值（不触发父节点更新）
                foreach (var kvp in counts)
                {
                    if (!_storage.TryGetValue(kvp.Key, out var node))
                        continue;

                    // 检查是否为叶子节点
                    if (!node.IsLeaf)
                    {
                        LogWarning($"[{Name}] SetCountBatch 应只对叶子节点调用，'{kvp.Key}' 是非叶子节点，已跳过");
                        continue;
                    }

                    var newCount = Math.Max(0, kvp.Value);
                    var oldCount = node.Count;

                    if (oldCount == newCount)
                        continue;

                    // 记录叶子节点变更
                    changeDict[kvp.Key] = new RedDotChangeInfo
                    {
                        Key = kvp.Key,
                        OldCount = oldCount,
                        NewCount = newCount,
                        Type = node.Type
                    };

                    node.Count = newCount;
                    node.IsCacheValid = false;
                }

                // 2. 收集所有受影响的父节点的旧值
                var affectedParents = new Dictionary<string, int>();
                foreach (var key in counts.Keys)
                {
                    if (!_storage.TryGetValue(key, out var node))
                        continue;

                    var parentKey = node.ParentKey;
                    while (!string.IsNullOrEmpty(parentKey) && _storage.TryGetValue(parentKey, out var parentNode))
                    {
                        if (!affectedParents.ContainsKey(parentKey))
                        {
                            affectedParents[parentKey] = parentNode.CachedTotalCount;
                        }
                        parentKey = parentNode.ParentKey;
                    }
                }

                // 3. 从根节点统一重新计算一次
                var rootNodes = _storage.Values.Where(n => string.IsNullOrEmpty(n.ParentKey)).ToList();
                foreach (var root in rootNodes)
                {
                    RecalculateInternal(root);
                }

                // 4. 收集父节点变更（与旧值对比）
                foreach (var kvp in affectedParents)
                {
                    if (_storage.TryGetValue(kvp.Key, out var parentNode))
                    {
                        var oldCount = kvp.Value;
                        var newCount = parentNode.CachedTotalCount;

                        if (oldCount != newCount)
                        {
                            changeDict[kvp.Key] = new RedDotChangeInfo
                            {
                                Key = kvp.Key,
                                OldCount = oldCount,
                                NewCount = newCount,
                                Type = parentNode.Type
                            };
                        }
                    }
                }
            }

            return changeDict.Values.ToList();
        }

        public int GetCount(string key)
        {
            if (!_globalEnabled)
                return 0;
            
            lock (_lock)
            {
                if (!_storage.TryGetValue(key, out var node))
                    return 0;

                // 检查节点及父节点是否被禁用
                if (!IsNodeEnabled(node))
                    return 0;

                if (node.IsCacheValid)
                    return node.CachedTotalCount;

                return RecalculateInternal(node);
            }
        }

        public void SetEnabled(string key, bool enabled)
        {
            lock (_lock)
            {
                if (!_storage.TryGetValue(key, out var node))
                    return;

                node.IsEnabled = enabled;
            }
        }

        public bool GetEnabled(string key)
        {
            lock (_lock)
            {
                if (!_storage.TryGetValue(key, out var node))
                    return false;

                return IsNodeEnabled(node);
            }
        }

        public void SetAllEnabled(bool enabled)
        {
            _globalEnabled = enabled;
        }

        public bool GetAllEnabled()
        {
            return _globalEnabled;
        }

        public void InvalidateCache(string key)
        {
            lock (_lock)
            {
                if (_storage.TryGetValue(key, out var node))
                {
                    node.IsCacheValid = false;
                    InvalidateCacheUpward(node.ParentKey);
                }
            }
        }

        public void InvalidateAllCache()
        {
            lock (_lock)
            {
                foreach (var node in _storage.Values)
                {
                    node.IsCacheValid = false;
                }
            }
        }

        public int Recalculate(string key)
        {
            lock (_lock)
            {
                if (!_storage.TryGetValue(key, out var node))
                    return 0;

                return RecalculateInternal(node);
            }
        }

        #endregion

        #region 数据导入导出

        public Dictionary<string, int> Export()
        {
            var result = new Dictionary<string, int>();

            lock (_lock)
            {
                foreach (var node in _storage.Values)
                {
                    if (node.IsLeaf && node.Count > 0)
                    {
                        result[node.Key] = node.Count;
                    }
                }
            }

            return result;
        }

        public void Import(Dictionary<string, int> stateData)
        {
            if (stateData == null) return;

            lock (_lock)
            {
                // 只清零叶子节点
                foreach (var node in _storage.Values)
                {
                    if (node.IsLeaf)
                    {
                        node.Count = 0;
                        if (stateData.TryGetValue(node.Key, out var count))
                        {
                            node.Count = count;
                        }
                    }
                }

                // 从根节点重新计算（自动更新所有父节点的缓存）
                var rootNodes = _storage.Values.Where(n => string.IsNullOrEmpty(n.ParentKey)).ToList();
                foreach (var root in rootNodes)
                {
                    RecalculateInternal(root);
                }
            }

            Log($"[{Name}] 导入红点状态完成，共 {stateData.Count} 条");
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 检查节点是否启用（包括检查所有父节点）
        /// </summary>
        private bool IsNodeEnabled(RedDotNode node)
        {
            if (!node.IsEnabled)
                return false;

            // 检查所有父节点
            var parentKey = node.ParentKey;
            while (!string.IsNullOrEmpty(parentKey) && _storage.TryGetValue(parentKey, out var parentNode))
            {
                if (!parentNode.IsEnabled)
                    return false;
                parentKey = parentNode.ParentKey;
            }

            return true;
        }

        private void InvalidateCacheUpward(string key)
        {
            while (!string.IsNullOrEmpty(key) && _storage.TryGetValue(key, out var node))
            {
                node.IsCacheValid = false;
                key = node.ParentKey;
            }
        }

        private List<RedDotChangeInfo> UpdateParentCounts(string parentKey)
        {
            var changes = new List<RedDotChangeInfo>();

            while (!string.IsNullOrEmpty(parentKey) && _storage.TryGetValue(parentKey, out var parentNode))
            {
                var oldCount = parentNode.CachedTotalCount;
                var newCount = RecalculateInternal(parentNode);

                if (oldCount != newCount)
                {
                    changes.Add(new RedDotChangeInfo
                    {
                        Key = parentKey,
                        OldCount = oldCount,
                        NewCount = newCount,
                        Type = parentNode.Type
                    });
                }

                parentKey = parentNode.ParentKey;
            }

            return changes;
        }

        private int RecalculateInternal(RedDotNode node)
        {
            if (node.IsLeaf)
            {
                node.CachedTotalCount = node.Count;
            }
            else
            {
                var total = node.Count;
                foreach (var childKey in node.ChildKeys)
                {
                    if (_storage.TryGetValue(childKey, out var childNode))
                    {
                        total += childNode.IsCacheValid ? childNode.CachedTotalCount : RecalculateInternal(childNode);
                    }
                }
                node.CachedTotalCount = total;
            }

            node.IsCacheValid = true;
            return node.CachedTotalCount;
        }

        #endregion
    }
}
