using System;
using JulyCore.Core;
using JulyCore.Core.Events;
using JulyCore.Module.Guide;

namespace JulyCore
{
    public static partial class GF
    {
        /// <summary>
        /// 引导系统
        /// 职责：流程执行、状态管理、进度持久化
        /// 触发时机由项目层决定
        /// </summary>
        public static class Guide
        {
            private static GuideModule _module;
            private static GuideModule Module
            {
                get
                {
                    _module ??= GetModule<GuideModule>();
                    return _module;
                }
            }

            #region 处理器注入

            /// <summary>
            /// 设置表现适配器
            /// 由项目层实现，用于处理引导的UI表现
            /// </summary>
            public static void SetPresenter(IGuidePresenter presenter)
            {
                Module.SetPresenter(presenter);
            }

            /// <summary>
            /// 设置数据提供器
            /// 由项目层实现，用于提供引导配置数据
            /// </summary>
            public static void SetDataProvider(IGuideDataProvider provider)
            {
                Module.SetDataProvider(provider);
            }

            /// <summary>
            /// 注册流程处理器
            /// 每个 Flow 对应一个 Handler，负责监听事件并完成步骤
            /// 如果流程已完成，则跳过注册
            /// </summary>
            /// <param name="handler">流程处理器</param>
            /// <returns>是否成功注册（流程已完成时返回 false）</returns>
            public static bool RegisterFlowHandler(IGuideFlowHandler handler)
            {
                return Module.RegisterFlowHandler(handler);
            }

            /// <summary>
            /// 注销流程处理器
            /// </summary>
            /// <param name="flowId">流程ID</param>
            public static void UnregisterFlowHandler(string flowId)
            {
                Module.UnregisterFlowHandler(flowId);
            }

            #endregion

            #region 流程控制

            /// <summary>
            /// 启动引导流程
            /// - 已完成 → 返回 false
            /// - 已有流程运行 → 返回 false
            /// - 有中断进度（同一流程）→ 自动恢复
            /// - 有中断进度（其他流程）→ 返回 false
            /// </summary>
            /// <param name="flowId">流程ID</param>
            /// <returns>是否成功启动/恢复</returns>
            public static bool Start(string flowId)
            {
                return Module.StartFlow(flowId);
            }

            /// <summary>
            /// 完成当前步骤，自动推进到下一步
            /// </summary>
            public static void Complete()
            {
                Module.CompleteCurrentStep();
            }

            /// <summary>
            /// 跳过当前步骤，自动推进到下一步
            /// </summary>
            public static void SkipStep()
            {
                Module.SkipCurrentStep();
            }

            /// <summary>
            /// 跳过当前流程（用户主动跳过，标记为已完成，不再触发）
            /// </summary>
            /// <param name="reason">跳过原因（可选）</param>
            public static void SkipFlow(string reason = null)
            {
                Module.SkipCurrentFlow(reason);
            }

            #endregion

            #region 状态查询

            /// <summary>
            /// 是否有流程正在运行
            /// </summary>
            public static bool IsRunning()
            {
                return Module.IsFlowRunning();
            }

            /// <summary>
            /// 获取当前流程ID
            /// </summary>
            /// <returns>当前流程ID，无进行中流程返回null</returns>
            public static string GetCurrentFlowId()
            {
                return Module.GetCurrentFlowId();
            }

            /// <summary>
            /// 获取当前步骤ID
            /// </summary>
            /// <returns>当前步骤ID，无进行中步骤返回null</returns>
            public static string GetCurrentStepId()
            {
                return Module.GetCurrentStepId();
            }

            /// <summary>
            /// 检查流程是否已完成
            /// </summary>
            /// <param name="flowId">流程ID</param>
            /// <returns>是否已完成</returns>
            public static bool IsFlowCompleted(string flowId)
            {
                return Module.IsFlowCompleted(flowId);
            }

            #endregion

            #region 进度管理

            /// <summary>
            /// 清除所有进度（调试用）
            /// </summary>
            public static void ClearProgress()
            {
                Module.ClearProgress();
            }

            #endregion

            #region 事件订阅

            /// <summary>
            /// 订阅流程开始事件
            /// </summary>
            public static void OnFlowStarted(Action<GuideFlowStartedEvent> handler, object target)
            {
                _context.EventBus.Subscribe(handler, target);
            }

            /// <summary>
            /// 取消订阅流程开始事件
            /// </summary>
            public static void OffFlowStarted(Action<GuideFlowStartedEvent> handler)
            {
                _context.EventBus.Unsubscribe(handler);
            }

            /// <summary>
            /// 订阅流程完成事件
            /// </summary>
            public static void OnFlowCompleted(Action<GuideFlowCompletedEvent> handler, object target)
            {
                _context.EventBus.Subscribe(handler, target);
            }

            /// <summary>
            /// 取消订阅流程完成事件
            /// </summary>
            public static void OffFlowCompleted(Action<GuideFlowCompletedEvent> handler)
            {
                _context.EventBus.Unsubscribe(handler);
            }

            /// <summary>
            /// 订阅步骤进入事件
            /// </summary>
            public static void OnStepEntered(Action<GuideStepEnteredEvent> handler, object target)
            {
                _context.EventBus.Subscribe(handler, target);
            }

            /// <summary>
            /// 取消订阅步骤进入事件
            /// </summary>
            public static void OffStepEntered(Action<GuideStepEnteredEvent> handler)
            {
                _context.EventBus.Unsubscribe(handler);
            }

            /// <summary>
            /// 订阅步骤退出事件
            /// </summary>
            public static void OnStepExited(Action<GuideStepExitedEvent> handler, object target)
            {
                _context.EventBus.Subscribe(handler, target);
            }

            /// <summary>
            /// 取消订阅步骤退出事件
            /// </summary>
            public static void OffStepExited(Action<GuideStepExitedEvent> handler)
            {
                _context.EventBus.Unsubscribe(handler);
            }

            #endregion
        }
    }
}
