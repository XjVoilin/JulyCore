using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Provider.Base;
using JulyCore.Provider.Resource;

namespace JulyCore.Provider.Config
{
    /// <summary>
    /// 默认配置提供者实现
    /// 使用 LitJson 解析 JSON 格式配置
    /// </summary>
    internal class ConfigProvider : ProviderBase, IConfigProvider
    {
        public override int Priority => Frameworkconst.PriorityResourceProvider;
        protected override LogChannel LogChannel => LogChannel.Config;

        private readonly IResourceProvider _resourceProvider;
        private readonly Dictionary<Type, object> _tables = new();

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="resourceProvider">资源加载器</param>
        public ConfigProvider(IResourceProvider resourceProvider)
        {
            _resourceProvider = resourceProvider ?? throw new ArgumentNullException(nameof(resourceProvider));
        }

        /// <summary>
        /// 初始化（简单配置模式不需要整体初始化）
        /// </summary>
        public UniTask InitAsync(CancellationToken ct = default)
        {
            return UniTask.CompletedTask;
        }

        public UniTask InitAllAsync(CancellationToken token)
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 尝试获取配置表
        /// </summary>
        public bool TryGetTable<T>(out T table) where T : class
        {
            if (_tables.TryGetValue(typeof(T), out var obj) && obj is T t)
            {
                table = t;
                return true;
            }
            table = null;
            return false;
        }

        protected override void OnShutdown()
        {
            _tables.Clear();
        }
    }
}
