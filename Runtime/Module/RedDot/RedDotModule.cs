using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Core.Events;
using JulyCore.Data.RedDot;
using JulyCore.Module.Base;
using JulyCore.Provider.RedDot;

namespace JulyCore.Module.RedDot
{
    /// <summary>
    /// 红点模块：计算器与刷新；数据由 Provider 存储
    /// </summary>
    internal class RedDotModule : ModuleBase
    {
        private IRedDotProvider _provider;

        protected override LogChannel LogChannel => LogChannel.RedDot;

        private readonly Dictionary<string, RedDotValueCalculator> _calculators = new();

        private readonly object _lock = new();

        public override int Priority => Frameworkconst.PriorityRedDotModule;

        protected override UniTask OnInitAsync()
        {
            _provider = GetProvider<IRedDotProvider>();

            return base.OnInitAsync();
        }

        #region 节点注册

        internal bool RegisterNode(string key, string parentKey = null, RedDotType type = RedDotType.Normal)
        {
            return _provider.Store(key, parentKey, type);
        }

        internal void RegisterNodes(IEnumerable<(string Key, string ParentKey, RedDotType Type)> nodes)
        {
            _provider.StoreBatch(nodes);
        }

        internal bool UnregisterNode(string key)
        {
            lock (_lock)
            {
                _calculators.Remove(key);
            }

            return _provider.Remove(key);
        }

        internal void ClearAllNodes()
        {
            lock (_lock)
            {
                _calculators.Clear();
            }
            _provider.Clear();
        }

        #endregion

        #region 业务逻辑 - 计算器

        /// <summary>
        /// 设置红点值计算器
        /// 注意：只能对叶子节点设置计算器
        /// </summary>
        internal void SetCalculator(string key, RedDotValueCalculator calculator)
        {
            if (string.IsNullOrEmpty(key) || calculator == null)
                return;

            // 检查是否为叶子节点
            var node = _provider.Get(key);
            if (node == null)
            {
                LogWarning($"[{Name}] SetCalculator 失败：节点 '{key}' 不存在");
                return;
            }

            if (!node.IsLeaf)
            {
                LogWarning($"[{Name}] SetCalculator 应只对叶子节点设置，'{key}' 是非叶子节点，操作已忽略");
                return;
            }

            lock (_lock)
            {
                _calculators[key] = calculator;
            }
        }

        /// <summary>
        /// 设置红点值计算器（简化版）
        /// </summary>
        internal void SetCalculator(string key, Func<int> calculator)
        {
            SetCalculator(key, _ => calculator());
        }

        /// <summary>
        /// 移除计算器
        /// </summary>
        internal void RemoveCalculator(string key)
        {
            if (string.IsNullOrEmpty(key)) return;

            lock (_lock)
            {
                _calculators.Remove(key);
            }
        }

        /// <summary>
        /// 触发计算器重新计算
        /// </summary>
        internal List<RedDotChangeInfo> TriggerCalculator(string key)
        {
            RedDotValueCalculator calculator;
            lock (_lock)
            {
                if (!_calculators.TryGetValue(key, out calculator))
                    return new List<RedDotChangeInfo>();
            }

            try
            {
                var newValue = calculator(key);
                return _provider.SetCount(key, newValue);
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 红点计算器执行失败: {key}, 错误: {ex.Message}");
                return new List<RedDotChangeInfo>();
            }
        }

        /// <summary>
        /// 触发所有计算器重新计算（优化：使用批量操作，避免重复计算父节点）
        /// </summary>
        internal List<RedDotChangeInfo> TriggerAllCalculators()
        {
            // 1. 收集所有计算器
            Dictionary<string, RedDotValueCalculator> calculatorsCopy;
            lock (_lock)
            {
                calculatorsCopy = new Dictionary<string, RedDotValueCalculator>(_calculators);
            }

            if (calculatorsCopy.Count == 0)
                return new List<RedDotChangeInfo>();

            // 2. 批量计算所有节点的新值
            var counts = new Dictionary<string, int>(calculatorsCopy.Count);
            foreach (var kvp in calculatorsCopy)
            {
                try
                {
                    counts[kvp.Key] = kvp.Value(kvp.Key);
                }
                catch (Exception ex)
                {
                    LogError($"[{Name}] 红点计算器执行失败: {kvp.Key}, 错误: {ex.Message}");
                }
            }

            // 3. 使用批量设置（内部会统一计算父节点，避免重复）
            return _provider.SetCountBatch(counts);
        }

        /// <summary>
        /// 触发计算器并发布事件
        /// </summary>
        internal void Refresh(string key)
        {
            var changes = TriggerCalculator(key);
            PublishChanges(changes);
        }

        /// <summary>
        /// 刷新所有计算器并发布事件
        /// </summary>
        internal void RefreshAll()
        {
            var changes = TriggerAllCalculators();
            PublishChanges(changes);
        }

        #endregion

        #region 红点查询

        internal RedDotNode GetNode(string key) => _provider.Get(key);
        internal int GetCount(string key) => _provider.GetCount(key);

        /// <summary>
        /// 设置节点启用状态
        /// </summary>
        internal void SetEnabled(string key, bool enabled)
        {
            _provider.SetEnabled(key, enabled);
            EventBus.Publish(new RedDotEnabledChangedEvent { Key = key, Enabled = enabled });
        }

        /// <summary>
        /// 获取节点启用状态
        /// </summary>
        internal bool GetEnabled(string key)
        {
            return _provider.GetEnabled(key);
        }

        /// <summary>
        /// 设置全局启用状态
        /// </summary>
        internal void SetAllEnabled(bool enabled)
        {
            _provider.SetAllEnabled(enabled);
            EventBus.Publish(new RedDotEnabledChangedEvent { Key = null, Enabled = enabled });
        }

        /// <summary>
        /// 获取全局启用状态
        /// </summary>
        internal bool GetAllEnabled()
        {
            return _provider.GetAllEnabled();
        }

        #endregion

        #region 红点操作

        internal void Clear(string key)
        {
            var node = _provider.Get(key);
            if (node == null) return;

            if (node.IsLeaf)
            {
                // 叶子节点：直接清零
                var changes = _provider.SetCount(key, 0);
                PublishChanges(changes);
            }
            else
            {
                // 非叶子节点：递归清零所有子叶子节点
                var leafKeys = GetLeafKeysRecursive(key);
                if (leafKeys.Count == 0) return;

                var counts = new Dictionary<string, int>(leafKeys.Count);
                foreach (var leafKey in leafKeys)
                {
                    counts[leafKey] = 0;
                }

                var changes = _provider.SetCountBatch(counts);
                PublishChanges(changes);
            }
        }

        /// <summary>
        /// 递归获取指定节点下的所有叶子节点 Key
        /// </summary>
        private List<string> GetLeafKeysRecursive(string key)
        {
            var result = new List<string>();
            var node = _provider.Get(key);
            if (node == null) return result;

            if (node.IsLeaf)
            {
                result.Add(key);
            }
            else
            {
                var children = _provider.GetChildren(key);
                foreach (var child in children)
                {
                    result.AddRange(GetLeafKeysRecursive(child.Key));
                }
            }

            return result;
        }

        internal void ClearAll()
        {
            // 使用批量操作清零所有叶子节点
            var leafNodes = _provider.GetLeafNodes();
            var counts = new Dictionary<string, int>(leafNodes.Count);
            foreach (var leaf in leafNodes)
            {
                counts[leaf.Key] = 0;
            }
            
            var changes = _provider.SetCountBatch(counts);
            PublishChanges(changes);
        }

        #endregion

        #region 数据持久化

        internal Dictionary<string, int> ExportState() => _provider.Export();
        internal void ImportState(Dictionary<string, int> stateData) => _provider.Import(stateData);

        #endregion

        #region 配置加载

        internal void LoadFromConfig(RedDotNodeConfig config)
        {
            if (config == null) return;
            _provider.Store(config.Key, config.ParentKey, config.Type);
        }

        internal void LoadFromConfigTable(RedDotConfigTable configTable)
        {
            if (configTable?.Nodes == null) return;
            _provider.StoreBatch(configTable.ToRegistrations());
        }

        #endregion

        private void PublishChanges(List<RedDotChangeInfo> changes)
        {
            if (changes == null || changes.Count == 0)
                return;

            try
            {
                foreach (var change in changes)
                {
                    EventBus.Publish(new RedDotChangedEvent
                    {
                        Key = change.Key,
                        OldCount = change.OldCount,
                        NewCount = change.NewCount,
                        Type = change.Type
                    });
                }
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 发布红点变更事件失败: {ex.Message}");
            }
        }

        protected override void OnShutdown()
        {
            _provider = null;
            lock (_lock)
            {
                _calculators.Clear();
            }
        }
    }
}
