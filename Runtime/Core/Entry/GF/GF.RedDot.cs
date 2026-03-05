using System;
using System.Collections.Generic;
using JulyCore.Core;
using JulyCore.Core.Events;
using JulyCore.Data.RedDot;
using JulyCore.Module.RedDot;

namespace JulyCore
{
    public static partial class GF
    {
        /// <summary>
        /// 红点相关操作
        /// </summary>
        public static class RedDot
        {
            private static RedDotModule _module;
            private static RedDotModule Module
            {
                get
                {
                    _module ??= GetModule<RedDotModule>();
                    return _module;
                }
            }
            
            #region 节点注册

            public static bool Register(string key, string parentKey = null, RedDotType type = RedDotType.Normal)
            {
                return Module.RegisterNode(key, parentKey, type);
            }

            public static void RegisterBatch(IEnumerable<(string Key, string ParentKey, RedDotType Type)> nodes)
            {
                Module.RegisterNodes(nodes);
            }

            public static bool Unregister(string key)
            {
                return Module.UnregisterNode(key);
            }

            public static void ClearAllNodes()
            {
                Module.ClearAllNodes();
            }

            public static void LoadFromConfig(RedDotNodeConfig config)
            {
                Module.LoadFromConfig(config);
            }

            public static void LoadFromConfigTable(RedDotConfigTable configTable)
            {
                Module.LoadFromConfigTable(configTable);
            }

            public static void LoadFromBuilder(Action<RedDotConfigBuilder> builderAction)
            {
                if (builderAction == null) return;
                var builder = new RedDotConfigBuilder();
                builderAction(builder);
                LoadFromConfigTable(builder.Build());
            }

            #endregion

            #region 系统关联

            public static void BindToSystem(string systemName, params string[] nodeKeys)
            {
                Module.BindToSystem(systemName, nodeKeys);
            }

            public static void UnbindSystem(string systemName)
            {
                Module.UnbindSystem(systemName);
            }

            public static void RefreshSystem(string systemName)
            {
                Module.RefreshSystem(systemName);
            }

            public static int GetSystemCount(string systemName)
            {
                return Module.GetSystemCount(systemName);
            }

            #endregion

            #region 红点查询

            public static RedDotNode GetNode(string key)
            {
                return Module.GetNode(key);
            }

            public static int GetCount(string key)
            {
                return Module.GetCount(key);
            }

            public static bool IsVisible(string key)
            {
                return Module.IsVisible(key);
            }

            public static List<RedDotNode> GetAllNodes()
            {
                return Module.GetAllNodes() ?? new List<RedDotNode>();
            }

            public static List<RedDotNode> GetChildNodes(string key)
            {
                return Module.GetChildNodes(key) ?? new List<RedDotNode>();
            }

            /// <summary>
            /// 设置节点启用状态
            /// 禁用后该节点及其子节点的红点不显示（GetCount 返回 0）
            /// </summary>
            /// <param name="key">节点 Key</param>
            /// <param name="enabled">是否启用</param>
            public static void SetEnabled(string key, bool enabled)
            {
                Module.SetEnabled(key, enabled);
            }

            /// <summary>
            /// 获取节点启用状态（包括检查父节点）
            /// </summary>
            /// <param name="key">节点 Key</param>
            /// <returns>是否启用</returns>
            public static bool GetEnabled(string key)
            {
                return Module.GetEnabled(key);
            }

            /// <summary>
            /// 设置全局启用状态（影响所有红点）
            /// 高性能：仅设置一个布尔值，GetCount 时 O(1) 检查
            /// </summary>
            /// <param name="enabled">是否启用</param>
            public static void SetAllEnabled(bool enabled)
            {
                Module.SetAllEnabled(enabled);
            }

            /// <summary>
            /// 获取全局启用状态
            /// </summary>
            /// <returns>是否启用</returns>
            public static bool GetAllEnabled()
            {
                return Module.GetAllEnabled();
            }

            #endregion

            #region 红点操作

            /// <summary>
            /// 清除指定节点的红点（标记为已读）
            /// </summary>
            public static void Clear(string key)
            {
                Module.Clear(key);
            }

            /// <summary>
            /// 清除所有红点
            /// </summary>
            public static void ClearAll()
            {
                Module.ClearAll();
            }

            /// <summary>
            /// 标记为已读（清零红点）
            /// </summary>
            public static void MarkAsRead(string key)
            {
                Module.MarkAsRead(key);
            }

            /// <summary>
            /// 标记为未读（设置红点为1）
            /// 注意：如果设置了计算器，下次 Refresh 会覆盖此值
            /// </summary>
            public static void MarkAsUnread(string key)
            {
                Module.MarkAsUnread(key);
            }

            #endregion

            #region 计算器

            public static void SetCalculator(string key, RedDotValueCalculator calculator)
            {
                Module.SetCalculator(key, calculator);
            }

            public static void SetCalculator(string key, Func<int> calculator)
            {
                Module.SetCalculator(key, calculator);
            }

            public static void RemoveCalculator(string key)
            {
                Module.RemoveCalculator(key);
            }

            public static void Refresh(string key)
            {
                Module.Refresh(key);
            }

            public static void RefreshAll()
            {
                Module.RefreshAll();
            }

            #endregion

            #region 数据持久化

            public static Dictionary<string, int> ExportState()
            {
                return Module.ExportState() ?? new Dictionary<string, int>();
            }

            public static void ImportState(Dictionary<string, int> stateData)
            {
                Module.ImportState(stateData);
            }

            #endregion

            #region 事件订阅

            public static void OnChanged(Action<RedDotChangedEvent> handler, object target)
            {
                _context.EventBus.Subscribe(handler, target);
            }

            public static void OnBatchChanged(Action<RedDotBatchChangedEvent> handler, object target)
            {
                _context.EventBus.Subscribe(handler, target);
            }

            public static void OnKeyChanged(string key, Action<RedDotChangedEvent> handler, object target)
            {
                _context.EventBus.Subscribe<RedDotChangedEvent>(evt =>
                {
                    if (evt.Key == key)
                        handler(evt);
                }, target);
            }

            public static void OffChanged(Action<RedDotChangedEvent> handler)
            {
                _context.EventBus.Unsubscribe(handler);
            }

            public static void OffBatchChanged(Action<RedDotBatchChangedEvent> handler)
            {
                _context.EventBus.Unsubscribe(handler);
            }

            public static void OnEnabledChanged(Action<RedDotEnabledChangedEvent> handler, object target)
            {
                _context.EventBus.Subscribe(handler, target);
            }

            public static void OffEnabledChanged(Action<RedDotEnabledChangedEvent> handler)
            {
                _context.EventBus.Unsubscribe(handler);
            }

            #endregion
        }
    }
}
