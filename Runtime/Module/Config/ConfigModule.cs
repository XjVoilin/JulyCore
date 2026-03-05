using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Module.Base;
using JulyCore.Provider.Config;

namespace JulyCore.Module.Config
{
    /// <summary>
    /// 配置模块
    /// 提供配置表的注册和访问能力
    /// </summary>
    internal class ConfigModule : ModuleBase
    {
        private IConfigProvider _provider;

        protected override LogChannel LogChannel => LogChannel.Config;
        public override int Priority => Frameworkconst.PriorityConfigModule;

        protected override async UniTask OnInitAsync()
        {
            _provider = GetProvider<IConfigProvider>();
            await base.OnInitAsync();
        }

        /// <summary>
        /// 尝试获取配置表
        /// </summary>
        public bool TryGetTable<T>(out T table) where T : class
        {
            return _provider.TryGetTable(out table);
        }
    }
}
