using System.Collections.Generic;
using JulyCore.Data.Guide;

namespace JulyCore.Core
{
    /// <summary>
    /// 引导数据提供器接口
    /// 由项目层实现，用于提供引导配置数据（可来自Excel/ScriptableObject/JSON/服务器等）
    /// </summary>
    public interface IGuideDataProvider
    {
        /// <summary>
        /// 获取指定流程数据
        /// </summary>
        /// <param name="flowId">流程ID</param>
        /// <returns>流程数据，不存在返回null</returns>
        GuideFlowData GetFlow(string flowId);

        /// <summary>
        /// 获取指定步骤数据
        /// </summary>
        /// <param name="flowId">流程ID</param>
        /// <param name="stepId">步骤ID</param>
        /// <returns>步骤数据，不存在返回null</returns>
        GuideStepData GetStep(string flowId, string stepId);

        /// <summary>
        /// 获取所有流程数据
        /// </summary>
        /// <returns>流程数据列表</returns>
        IReadOnlyList<GuideFlowData> GetAllFlows();
    }
}

