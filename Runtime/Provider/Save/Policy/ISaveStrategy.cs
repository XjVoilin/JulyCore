using JulyCore.Data.Save;

namespace JulyCore.Provider.Save
{
    /// <summary>
    /// 保存决策策略接口
    /// </summary>
    public interface ISaveStrategy
    {
        /// <summary>
        /// 判断数据是否应该被保存
        /// </summary>
        /// <param name="context">保存上下文</param>
        /// <returns>true 表示应该保存，false 表示跳过</returns>
        bool ShouldSave(SaveContext context);
    }
}

