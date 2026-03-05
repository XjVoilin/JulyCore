using System;

namespace JulyCore.Core
{
    /// <summary>
    /// 引导流程处理器接口
    /// 每个 Flow 对应一个 Handler，负责：
    /// 1. 监听触发条件，决定何时启动 Flow
    /// 2. 监听步骤完成事件，推进引导进度
    /// 
    /// 【生命周期】
    /// 1. OnRegister() - 注册时调用，监听触发条件
    /// 2. OnFlowStart() - Flow 启动时调用，监听步骤事件
    /// 3. Dispose() - Flow 结束后框架自动调用，清理所有订阅
    /// </summary>
    public interface IGuideFlowHandler : IDisposable
    {
        /// <summary>
        /// 对应的流程ID
        /// </summary>
        string FlowId { get; }

        /// <summary>
        /// 注册时调用
        /// 在此订阅触发条件事件，条件满足时调用 GF.Guide.Start(FlowId)
        /// </summary>
        void OnRegister();

        /// <summary>
        /// 流程开始时调用
        /// 在此订阅步骤完成事件
        /// </summary>
        void OnFlowStart();
    }
}

