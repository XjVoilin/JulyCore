using System;
using System.Collections.Generic;

namespace JulyCore.Data.RedDot
{
    /// <summary>
    /// 红点节点配置
    /// </summary>
    [Serializable]
    public class RedDotNodeConfig
    {
        public string Key { get; set; }
        public string ParentKey { get; set; }
        public RedDotType Type { get; set; } = RedDotType.Normal;
        public string SystemName { get; set; }
        public string Description { get; set; }

        public (string Key, string ParentKey, RedDotType Type) ToRegistration()
        {
            return (Key, ParentKey, Type);
        }
    }

    /// <summary>
    /// 红点配置表
    /// </summary>
    [Serializable]
    public class RedDotConfigTable
    {
        public List<RedDotNodeConfig> Nodes { get; set; } = new();

        public List<(string Key, string ParentKey, RedDotType Type)> ToRegistrations()
        {
            var result = new List<(string, string, RedDotType)>();
            if (Nodes != null)
            {
                foreach (var config in Nodes)
                {
                    result.Add(config.ToRegistration());
                }
            }
            return result;
        }

        public Dictionary<string, List<string>> GetSystemBindings()
        {
            var result = new Dictionary<string, List<string>>();
            if (Nodes != null)
            {
                foreach (var config in Nodes)
                {
                    if (!string.IsNullOrEmpty(config.SystemName))
                    {
                        if (!result.TryGetValue(config.SystemName, out var list))
                        {
                            list = new List<string>();
                            result[config.SystemName] = list;
                        }
                        list.Add(config.Key);
                    }
                }
            }
            return result;
        }
    }

    /// <summary>
    /// 红点树配置构建器
    /// </summary>
    public class RedDotConfigBuilder
    {
        private readonly RedDotConfigTable _configTable = new();
        private readonly Dictionary<string, RedDotNodeConfig> _nodeMap = new();

        public RedDotConfigBuilder AddRoot(string key, RedDotType type = RedDotType.Normal,
            string systemName = null, string description = null)
        {
            return AddNode(key, null, type, systemName, description);
        }

        public RedDotConfigBuilder AddNode(string key, string parentKey,
            RedDotType type = RedDotType.Normal, string systemName = null, string description = null)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("节点Key不能为空", nameof(key));

            if (_nodeMap.ContainsKey(key))
                throw new InvalidOperationException($"节点 {key} 已存在");

            var config = new RedDotNodeConfig
            {
                Key = key,
                ParentKey = parentKey,
                Type = type,
                SystemName = systemName,
                Description = description
            };

            _configTable.Nodes.Add(config);
            _nodeMap[key] = config;
            return this;
        }

        public RedDotConfigBuilder AddChildTo(string parentKey, string childKey,
            RedDotType type = RedDotType.Normal, string systemName = null, string description = null)
        {
            return AddNode(childKey, parentKey, type, systemName, description);
        }

        public RedDotConfigBuilder AddChildrenTo(string parentKey, IEnumerable<string> childKeys,
            RedDotType type = RedDotType.Normal)
        {
            foreach (var childKey in childKeys)
            {
                AddNode(childKey, parentKey, type);
            }
            return this;
        }

        public RedDotConfigTable Build()
        {
            return _configTable;
        }
    }
}

