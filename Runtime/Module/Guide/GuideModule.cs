using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Core.Events;
using JulyCore.Data.Guide;
using JulyCore.Module.Base;
using JulyCore.Provider.Guide;
using JulyCore.Provider.Save;

namespace JulyCore.Module.Guide
{
    /// <summary>
    /// 引导模块
    /// 
    /// 【职责】
    /// - 业务流程控制：启动、跳过、完成流程/步骤
    /// - 运行态管理：_isFlowActive、_activeHandler
    /// - Handler 生命周期管理
    /// 
    /// 【依赖】
    /// - IGuideProvider：内存中的进度状态管理
    /// - ISaveProvider：进度数据的持久化
    /// </summary>
    internal class GuideModule : ModuleBase
    {
        private IGuideProvider _guideProvider;
        private ISaveProvider _saveProvider;
        private GuideProgressData _progressData;

        protected override LogChannel LogChannel => LogChannel.Guide;

        private const string SAVE_KEY = Frameworkconst.GuideSaveKey;

        private IGuidePresenter _presenter;
        private IGuideDataProvider _dataProvider;

        /// <summary>
        /// 流程处理器字典
        /// </summary>
        private readonly Dictionary<string, IGuideFlowHandler> _flowHandlers = new();

        /// <summary>
        /// 当前活跃的 Handler
        /// </summary>
        private IGuideFlowHandler _activeHandler;

        /// <summary>
        /// 标记当前是否有流程处于活跃状态
        /// </summary>
        private bool _isFlowActive;

        public override int Priority => Frameworkconst.PriorityGuideModule;

        #region 生命周期

        protected override async UniTask OnInitAsync()
        {
            _guideProvider = GetProvider<IGuideProvider>();
            _saveProvider = GetProvider<ISaveProvider>();

            await LoadProgressInternal();

            Log($"[{Name}] 引导模块初始化完成");
        }

        protected override async UniTask OnShutdownAsync()
        {
            // 关闭时保存进度
            await SaveProgressInternal();

            // 清理所有 Handler
            foreach (var handler in _flowHandlers.Values)
            {
                try
                {
                    handler.Dispose();
                }
                catch (Exception ex)
                {
                    LogError($"[{Name}] Handler 释放失败: {ex.Message}");
                }
            }
            _flowHandlers.Clear();
            _activeHandler = null;

            _presenter = null;
            _dataProvider = null;
            _saveProvider = null;
            _progressData = null;

            Log($"[{Name}] 引导模块已关闭");
        }

        #endregion

        #region 处理器注入

        /// <summary>
        /// 设置表现适配器
        /// </summary>
        internal void SetPresenter(IGuidePresenter presenter)
        {
            _presenter = presenter;
            Log($"[{Name}] 表现适配器已设置: {presenter?.GetType().Name ?? "null"}");
        }

        /// <summary>
        /// 设置数据提供器
        /// </summary>
        internal void SetDataProvider(IGuideDataProvider provider)
        {
            _dataProvider = provider;
            Log($"[{Name}] 数据提供器已设置: {provider?.GetType().Name ?? "null"}");
        }

        /// <summary>
        /// 注册流程处理器
        /// 如果流程已完成，则跳过注册
        /// 注册后立即调用 OnRegister()，开始监听触发条件
        /// </summary>
        /// <returns>是否成功注册</returns>
        internal bool RegisterFlowHandler(IGuideFlowHandler handler)
        {
            if (handler == null)
            {
                LogWarning($"[{Name}] 注册 Handler 失败：handler 为空");
                return false;
            }

            if (string.IsNullOrEmpty(handler.FlowId))
            {
                LogWarning($"[{Name}] 注册 Handler 失败：FlowId 为空");
                return false;
            }

            // 如果流程已完成，跳过注册
            if (_guideProvider.IsFlowCompleted(handler.FlowId))
            {
                Log($"[{Name}] 流程已完成，跳过 Handler 注册: {handler.FlowId}");
                handler.Dispose();
                return false;
            }

            // 如果已存在，先释放旧的
            if (_flowHandlers.TryGetValue(handler.FlowId, out var oldHandler))
            {
                LogWarning($"[{Name}] Handler 已存在，将被覆盖: {handler.FlowId}");
                try
                {
                    oldHandler.Dispose();
                }
                catch (Exception ex)
                {
                    LogError($"[{Name}] 旧 Handler 释放异常: {ex.Message}");
                }
            }

            _flowHandlers[handler.FlowId] = handler;

            // 调用 OnRegister，开始监听触发条件
            try
            {
                handler.OnRegister();
                Log($"[{Name}] Handler 已注册并激活触发监听: {handler.FlowId} ({handler.GetType().Name})");
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] Handler.OnRegister 异常: {ex.Message}");
            }

            return true;
        }

        /// <summary>
        /// 注销流程处理器
        /// </summary>
        internal void UnregisterFlowHandler(string flowId)
        {
            if (_flowHandlers.TryGetValue(flowId, out var handler))
            {
                try
                {
                    handler.Dispose();
                }
                catch (Exception ex)
                {
                    LogError($"[{Name}] Handler 释放异常: {ex.Message}");
                }
                _flowHandlers.Remove(flowId);
                Log($"[{Name}] Handler 已注销: {flowId}");
            }
        }

        #endregion

        #region 流程控制

        /// <summary>
        /// 启动引导流程
        /// </summary>
        internal bool StartFlow(string flowId)
        {
            if (string.IsNullOrEmpty(flowId))
            {
                LogWarning($"[{Name}] 启动流程失败：flowId 为空");
                return false;
            }

            // 检查是否已有活跃流程
            if (_isFlowActive)
            {
                LogWarning($"[{Name}] 启动流程失败：已有流程在运行");
                return false;
            }

            // 检查流程是否已完成
            if (_guideProvider.IsFlowCompleted(flowId))
            {
                Log($"[{Name}] 流程已完成，跳过启动: {flowId}");
                return false;
            }

            // 检查是否有中断的流程
            var interruptedFlowId = _guideProvider.GetCurrentFlowId();
            if (!string.IsNullOrEmpty(interruptedFlowId))
            {
                if (interruptedFlowId == flowId)
                {
                    return ResumeFlowInternal(flowId);
                }

                LogWarning($"[{Name}] 启动流程失败：有其他中断的流程待恢复 ({interruptedFlowId})");
                return false;
            }

            // 检查数据提供器
            if (_dataProvider == null)
            {
                LogError($"[{Name}] 启动流程失败：未设置数据提供器");
                return false;
            }

            // 获取流程数据
            var flowData = _dataProvider.GetFlow(flowId);
            if (flowData == null)
            {
                LogWarning($"[{Name}] 启动流程失败：找不到流程数据 ({flowId})");
                return false;
            }

            return StartFlowInternal(flowData);
        }

        /// <summary>
        /// 内部启动流程
        /// </summary>
        private bool StartFlowInternal(GuideFlowData flowData)
        {
            var flowId = flowData.FlowId;
            // 启动流程
            _guideProvider.SetFlowStatus(flowId, GuideFlowStatus.Running);
            _isFlowActive = true;

            // 激活 Handler
            ActivateHandler(flowId);

            // 通知表现层
            _presenter?.OnFlowStart(flowId);

            // 发布事件
            EventBus.Publish(new GuideFlowStartedEvent { FlowId = flowId });

            Log($"[{Name}] 流程已启动: {flowId}");

            // 查找恢复点
            var resumeStepId = FindResumeStepId(flowId, flowData.EntryStepId);

            if (resumeStepId == null)
            {
                CompleteFlow(flowId);
                return true;
            }

            AdvanceToStep(flowId, resumeStepId);
            return true;
        }

        /// <summary>
        /// 恢复中断的流程
        /// </summary>
        private bool ResumeFlowInternal(string flowId)
        {
            var stepId = _guideProvider.GetCurrentStepId();

            if (_dataProvider == null)
            {
                LogError($"[{Name}] 恢复流程失败：未设置数据提供器");
                return false;
            }

            var flowData = _dataProvider.GetFlow(flowId);
            if (flowData == null)
            {
                LogWarning($"[{Name}] 恢复流程失败：找不到流程数据 ({flowId})");
                _guideProvider.ClearCurrentStep();
                return false;
            }

            // 确定恢复的步骤
            string resumeStepId;
            if (!string.IsNullOrEmpty(stepId))
            {
                var stepData = _dataProvider.GetStep(flowId, stepId);
                resumeStepId = stepData != null ? stepId : FindResumeStepId(flowId, flowData.EntryStepId);
            }
            else
            {
                resumeStepId = FindResumeStepId(flowId, flowData.EntryStepId);
            }

            if (resumeStepId == null)
            {
                _isFlowActive = true;
                CompleteFlow(flowId);
                return true;
            }

            _guideProvider.SetFlowStatus(flowId, GuideFlowStatus.Running);
            _isFlowActive = true;

            // 激活 Handler
            ActivateHandler(flowId);

            _presenter?.OnFlowStart(flowId);
            EventBus.Publish(new GuideFlowStartedEvent { FlowId = flowId });

            Log($"[{Name}] 流程已恢复: {flowId}, 从步骤: {resumeStepId}");

            AdvanceToStep(flowId, resumeStepId);
            return true;
        }

        /// <summary>
        /// 查找恢复点
        /// </summary>
        private string FindResumeStepId(string flowId, string startStepId)
        {
            var currentStepId = startStepId;

            while (!string.IsNullOrEmpty(currentStepId))
            {
                if (!_guideProvider.IsStepCompleted(flowId, currentStepId))
                {
                    return currentStepId;
                }

                var stepData = _dataProvider.GetStep(flowId, currentStepId);
                if (stepData == null) break;

                currentStepId = stepData.NextStepId;
            }

            return null;
        }

        /// <summary>
        /// 跳过当前流程（用户主动跳过，标记为已完成）
        /// </summary>
        internal void SkipCurrentFlow(string reason = null)
        {
            var flowId = _guideProvider.GetCurrentFlowId();
            if (string.IsNullOrEmpty(flowId))
            {
                LogWarning($"[{Name}] 跳过流程失败：没有进行中的流程");
                return;
            }

            var stepId = _guideProvider.GetCurrentStepId();

            // 退出当前步骤
            if (!string.IsNullOrEmpty(stepId) && _dataProvider != null)
            {
                var stepData = _dataProvider.GetStep(flowId, stepId);
                if (stepData != null)
                {
                    _guideProvider.SetStepStatus(flowId, stepId, GuideStepStatus.Skipped);
                    _presenter?.OnStepExit(stepData);
                    EventBus.Publish(new GuideStepExitedEvent
                    {
                        FlowId = flowId,
                        StepId = stepId,
                        Completed = false
                    });
                }
            }

            // 释放 Handler
            DisposeHandler(flowId);

            // 标记为已完成（跳过 = 永久完成，不再触发）
            _guideProvider.MarkFlowCompleted(flowId);
            _guideProvider.ClearCurrentStep();
            _isFlowActive = false;

            _presenter?.OnFlowComplete(flowId);
            EventBus.Publish(new GuideFlowCompletedEvent { FlowId = flowId });

            SaveProgressInternal().Forget();

            Log($"[{Name}] 流程已跳过: {flowId}, 原因: {reason ?? "无"}");
        }

        /// <summary>
        /// 完成当前步骤
        /// </summary>
        internal void CompleteCurrentStep()
        {
            var flowId = _guideProvider.GetCurrentFlowId();
            var stepId = _guideProvider.GetCurrentStepId();

            if (string.IsNullOrEmpty(flowId) || string.IsNullOrEmpty(stepId))
            {
                LogWarning($"[{Name}] 完成步骤失败：没有进行中的步骤");
                return;
            }

            var stepData = _dataProvider.GetStep(flowId, stepId);
            if (stepData == null)
            {
                LogWarning($"[{Name}] 完成步骤失败：找不到步骤数据 ({flowId}/{stepId})");
                return;
            }

            _guideProvider.MarkStepCompleted(flowId, stepId);
            _presenter?.OnStepExit(stepData);
            EventBus.Publish(new GuideStepExitedEvent
            {
                FlowId = flowId,
                StepId = stepId,
                Completed = true
            });

            Log($"[{Name}] 步骤已完成: {flowId}/{stepId}");

            if (string.IsNullOrEmpty(stepData.NextStepId))
            {
                CompleteFlow(flowId);
            }
            else
            {
                AdvanceToStep(flowId, stepData.NextStepId);
            }
        }

        /// <summary>
        /// 跳过当前步骤
        /// </summary>
        internal void SkipCurrentStep()
        {
            var flowId = _guideProvider.GetCurrentFlowId();
            var stepId = _guideProvider.GetCurrentStepId();

            if (string.IsNullOrEmpty(flowId) || string.IsNullOrEmpty(stepId))
            {
                LogWarning($"[{Name}] 跳过步骤失败：没有进行中的步骤");
                return;
            }

            var stepData = _dataProvider.GetStep(flowId, stepId);
            if (stepData == null)
            {
                LogWarning($"[{Name}] 跳过步骤失败：找不到步骤数据 ({flowId}/{stepId})");
                return;
            }

            _guideProvider.SetStepStatus(flowId, stepId, GuideStepStatus.Skipped);
            _presenter?.OnStepExit(stepData);
            EventBus.Publish(new GuideStepExitedEvent
            {
                FlowId = flowId,
                StepId = stepId,
                Completed = false
            });

            Log($"[{Name}] 步骤已跳过: {flowId}/{stepId}");

            if (string.IsNullOrEmpty(stepData.NextStepId))
            {
                CompleteFlow(flowId);
            }
            else
            {
                AdvanceToStep(flowId, stepData.NextStepId);
            }
        }

        #endregion

        #region 状态查询

        internal bool IsFlowRunning() => _isFlowActive;
        internal string GetCurrentFlowId() => _guideProvider.GetCurrentFlowId();
        internal string GetCurrentStepId() => _guideProvider.GetCurrentStepId();
        internal bool IsFlowCompleted(string flowId) => _guideProvider.IsFlowCompleted(flowId);

        #endregion

        #region 进度管理

        internal void ClearProgress()
        {
            _guideProvider.ClearProgress();
            _isFlowActive = false;
            _progressData = null;
            _saveProvider.Unregister(SAVE_KEY);
            _saveProvider.Delete(SAVE_KEY);
            Log($"[{Name}] 引导进度已清除");
        }

        #endregion

        #region Handler 管理

        /// <summary>
        /// 激活 Handler
        /// </summary>
        private void ActivateHandler(string flowId)
        {
            if (_flowHandlers.TryGetValue(flowId, out var handler))
            {
                _activeHandler = handler;
                try
                {
                    handler.OnFlowStart();
                    Log($"[{Name}] Handler 已激活: {flowId}");
                }
                catch (Exception ex)
                {
                    LogError($"[{Name}] Handler.OnFlowStart 异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 释放 Handler（Flow 结束后调用）
        /// </summary>
        private void DisposeHandler(string flowId)
        {
            if (_activeHandler != null && _activeHandler.FlowId == flowId)
            {
                _activeHandler = null;
            }

            if (_flowHandlers.TryGetValue(flowId, out var handler))
            {
                try
                {
                    handler.Dispose();
                    Log($"[{Name}] Handler 已释放: {flowId}");
                }
                catch (Exception ex)
                {
                    LogError($"[{Name}] Handler.Dispose 异常: {ex.Message}");
                }
                _flowHandlers.Remove(flowId);
            }
        }

        #endregion

        #region 私有方法

        private async UniTask LoadProgressInternal()
        {
            try
            {
                _progressData = await _saveProvider.LoadAndRegisterAsync<GuideProgressData>(SAVE_KEY, GFCancellationToken);
                if (_progressData != null && (_progressData.completedFlows?.Count > 0 || !string.IsNullOrEmpty(_progressData.currentFlowId)))
                {
                    _guideProvider.ImportProgress(_progressData);
                    Log($"[{Name}] 加载引导进度成功");
                }
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 加载引导进度失败: {ex.Message}");
            }
        }

        private async UniTask SaveProgressInternal()
        {
            try
            {
                var data = _guideProvider.ExportProgress();
                
                if (_progressData != null)
                {
                    // 更新已注册数据的内容
                    _progressData.currentFlowId = data.currentFlowId;
                    _progressData.currentStepId = data.currentStepId;
                    _progressData.completedFlows = data.completedFlows;
                    _progressData.completedSteps = data.completedSteps;
                    
                    // 新手引导是关键数据，立即保存
                    _saveProvider.MarkDirty(SAVE_KEY);
                    var results = await _saveProvider.SaveRegisteredAsync(new[] { SAVE_KEY }, GFCancellationToken);
                    if (!results.TryGetValue(SAVE_KEY, out var result) || !result.Success)
                    {
                        LogWarning($"[{Name}] 引导进度保存失败");
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 保存引导进度失败: {ex.Message}");
            }
        }

        private void AdvanceToStep(string flowId, string stepId)
        {
            if (string.IsNullOrEmpty(stepId))
            {
                LogWarning($"[{Name}] 推进步骤失败：stepId 为空");
                CompleteFlow(flowId);
                return;
            }

            var stepData = _dataProvider.GetStep(flowId, stepId);
            if (stepData == null)
            {
                LogWarning($"[{Name}] 推进步骤失败：找不到步骤数据 ({flowId}/{stepId})");
                CompleteFlow(flowId);
                return;
            }

            _guideProvider.SetCurrentStep(flowId, stepId);
            _guideProvider.SetStepStatus(flowId, stepId, GuideStepStatus.Active);

            _presenter?.OnStepEnter(stepData);
            EventBus.Publish(new GuideStepEnteredEvent { FlowId = flowId, StepId = stepId });

            SaveProgressInternal().Forget();

            Log($"[{Name}] 进入步骤: {flowId}/{stepId}");
        }

        private void CompleteFlow(string flowId)
        {
            // 释放 Handler
            DisposeHandler(flowId);

            _guideProvider.MarkFlowCompleted(flowId);
            _guideProvider.ClearCurrentStep();
            _isFlowActive = false;

            _presenter?.OnFlowComplete(flowId);
            EventBus.Publish(new GuideFlowCompletedEvent { FlowId = flowId });

            SaveProgressInternal().Forget();

            Log($"[{Name}] 流程已完成: {flowId}");
        }

        #endregion
    }
}
