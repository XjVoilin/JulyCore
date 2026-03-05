using JulyCore.Core;

namespace JulyCore.Provider.Config
{
    /// <summary>
    /// 配置提供者接口
    /// 初始化配置和获取
    /// </summary>
    public interface IConfigProvider : IProvider
    {
        /// <summary>
        /// 尝试获取配置表
        /// </summary>
        bool TryGetTable<T>(out T table) where T : class;
    }
}
