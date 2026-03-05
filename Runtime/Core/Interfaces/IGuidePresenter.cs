using JulyCore.Data.Guide;

namespace JulyCore.Core
{
    /// <summary>
    /// 引导表现适配器接口
    /// 由项目层实现，用于处理引导的UI表现（遮罩、高亮、手指动画等）
    /// </summary>
    public interface IGuidePresenter
    {
        /// <summary>
        /// 步骤进入时调用
        /// </summary>
        /// <param name="step">步骤数据</param>
        void OnStepEnter(GuideStepData step);

        /// <summary>
        /// 步骤退出时调用
        /// </summary>
        /// <param name="step">步骤数据</param>
        void OnStepExit(GuideStepData step);

        /// <summary>
        /// 流程开始时调用
        /// </summary>
        /// <param name="flowId">流程ID</param>
        void OnFlowStart(string flowId);

        /// <summary>
        /// 流程完成时调用（包括正常完成和用户跳过）
        /// </summary>
        /// <param name="flowId">流程ID</param>
        void OnFlowComplete(string flowId);
    }
}

