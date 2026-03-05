namespace JulyCore.Core.Events
{
    /// <summary>
    /// 引导流程开始事件
    /// </summary>
    public struct GuideFlowStartedEvent : IEvent
    {
        /// <summary>
        /// 流程ID
        /// </summary>
        public string FlowId;
    }

    /// <summary>
    /// 引导流程完成事件（包括正常完成和用户跳过）
    /// </summary>
    public struct GuideFlowCompletedEvent : IEvent
    {
        /// <summary>
        /// 流程ID
        /// </summary>
        public string FlowId;
    }

    /// <summary>
    /// 引导步骤进入事件
    /// </summary>
    public struct GuideStepEnteredEvent : IEvent
    {
        /// <summary>
        /// 流程ID
        /// </summary>
        public string FlowId;

        /// <summary>
        /// 步骤ID
        /// </summary>
        public string StepId;
    }

    /// <summary>
    /// 引导步骤退出事件
    /// </summary>
    public struct GuideStepExitedEvent : IEvent
    {
        /// <summary>
        /// 流程ID
        /// </summary>
        public string FlowId;

        /// <summary>
        /// 步骤ID
        /// </summary>
        public string StepId;

        /// <summary>
        /// 是否完成（true=完成，false=跳过）
        /// </summary>
        public bool Completed;
    }
}

