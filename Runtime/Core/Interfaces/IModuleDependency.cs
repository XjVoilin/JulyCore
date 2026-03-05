using System.Collections.Generic;

namespace JulyCore.Core
{
    /// <summary>
    /// 模块依赖接口
    /// 实现此接口以声明模块依赖关系
    /// </summary>
    public interface IModuleDependency
    {
        /// <summary>
        /// 获取依赖的模块类型列表
        /// </summary>
        /// <returns>依赖的模块类型列表</returns>
        IEnumerable<System.Type> GetDependencies();
    }
}

