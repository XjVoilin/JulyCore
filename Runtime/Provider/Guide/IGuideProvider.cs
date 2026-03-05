using JulyCore.Core;
using JulyCore.Data.Guide;

namespace JulyCore.Provider.Guide
{
    /// <summary>
    /// 引导存储提供者接口
    /// 纯技术层：仅负责引导状态存储，不包含业务逻辑
    /// </summary>
    public interface IGuideProvider : IProvider
    {
        #region 流程状态

        /// <summary>
        /// 获取流程状态
        /// </summary>
        /// <param name="flowId">流程ID</param>
        /// <returns>流程状态</returns>
        GuideFlowStatus GetFlowStatus(string flowId);

        /// <summary>
        /// 设置流程状态
        /// </summary>
        /// <param name="flowId">流程ID</param>
        /// <param name="status">流程状态</param>
        void SetFlowStatus(string flowId, GuideFlowStatus status);

        #endregion

        #region 步骤状态

        /// <summary>
        /// 获取步骤状态
        /// </summary>
        /// <param name="flowId">流程ID</param>
        /// <param name="stepId">步骤ID</param>
        /// <returns>步骤状态</returns>
        GuideStepStatus GetStepStatus(string flowId, string stepId);

        /// <summary>
        /// 设置步骤状态
        /// </summary>
        /// <param name="flowId">流程ID</param>
        /// <param name="stepId">步骤ID</param>
        /// <param name="status">步骤状态</param>
        void SetStepStatus(string flowId, string stepId, GuideStepStatus status);

        #endregion

        #region 当前进度

        /// <summary>
        /// 获取当前流程ID
        /// </summary>
        /// <returns>当前流程ID，无进行中流程返回null</returns>
        string GetCurrentFlowId();

        /// <summary>
        /// 获取当前步骤ID
        /// </summary>
        /// <returns>当前步骤ID，无进行中步骤返回null</returns>
        string GetCurrentStepId();

        /// <summary>
        /// 设置当前步骤
        /// </summary>
        /// <param name="flowId">流程ID</param>
        /// <param name="stepId">步骤ID</param>
        void SetCurrentStep(string flowId, string stepId);

        /// <summary>
        /// 清除当前步骤
        /// </summary>
        void ClearCurrentStep();

        #endregion

        #region 完成标记

        /// <summary>
        /// 检查流程是否已完成
        /// </summary>
        /// <param name="flowId">流程ID</param>
        /// <returns>是否已完成</returns>
        bool IsFlowCompleted(string flowId);

        /// <summary>
        /// 检查步骤是否已完成
        /// </summary>
        /// <param name="flowId">流程ID</param>
        /// <param name="stepId">步骤ID</param>
        /// <returns>是否已完成</returns>
        bool IsStepCompleted(string flowId, string stepId);

        /// <summary>
        /// 标记流程已完成
        /// </summary>
        /// <param name="flowId">流程ID</param>
        void MarkFlowCompleted(string flowId);

        /// <summary>
        /// 标记步骤已完成
        /// </summary>
        /// <param name="flowId">流程ID</param>
        /// <param name="stepId">步骤ID</param>
        void MarkStepCompleted(string flowId, string stepId);

        #endregion

        #region 进度导入导出

        /// <summary>
        /// 导出进度数据
        /// </summary>
        /// <returns>进度数据</returns>
        GuideProgressData ExportProgress();

        /// <summary>
        /// 导入进度数据
        /// </summary>
        /// <param name="data">进度数据</param>
        void ImportProgress(GuideProgressData data);

        /// <summary>
        /// 清除所有进度
        /// </summary>
        void ClearProgress();

        #endregion
    }
}

