using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Data.Guide;
using JulyCore.Provider.Base;

namespace JulyCore.Provider.Guide
{
    /// <summary>
    /// 引导存储提供者实现
    /// 纯技术层：仅负责引导状态存储，不包含业务逻辑
    /// </summary>
    internal class GuideProvider : ProviderBase, IGuideProvider
    {
        public override int Priority => Frameworkconst.PriorityGuideProvider;
        protected override LogChannel LogChannel => LogChannel.Guide;

        // 流程状态存储
        private readonly Dictionary<string, GuideFlowStatus> _flowStatuses = new();

        // 步骤状态存储
        private readonly Dictionary<string, GuideStepStatus> _stepStatuses = new();

        // 已完成的流程
        private readonly HashSet<string> _completedFlows = new();

        // 已完成的步骤
        private readonly HashSet<string> _completedSteps = new();

        // 当前进度
        private string _currentFlowId;
        private string _currentStepId;

        private readonly object _lock = new();

        protected override UniTask OnInitAsync()
        {
            return UniTask.CompletedTask;
        }

        protected override void OnShutdown()
        {
            lock (_lock)
            {
                _flowStatuses.Clear();
                _stepStatuses.Clear();
                _completedFlows.Clear();
                _completedSteps.Clear();
                _currentFlowId = null;
                _currentStepId = null;
            }
        }

        #region 流程状态

        public GuideFlowStatus GetFlowStatus(string flowId)
        {
            if (string.IsNullOrEmpty(flowId)) return GuideFlowStatus.Idle;

            lock (_lock)
            {
                return _flowStatuses.GetValueOrDefault(flowId, GuideFlowStatus.Idle);
            }
        }

        public void SetFlowStatus(string flowId, GuideFlowStatus status)
        {
            if (string.IsNullOrEmpty(flowId)) return;

            lock (_lock)
            {
                _flowStatuses[flowId] = status;
                if (status == GuideFlowStatus.Completed)
                {
                    _completedFlows.Add(flowId);
                }
            }
        }

        #endregion

        #region 步骤状态

        public GuideStepStatus GetStepStatus(string flowId, string stepId)
        {
            if (string.IsNullOrEmpty(flowId) || string.IsNullOrEmpty(stepId))
                return GuideStepStatus.Pending;

            var key = GetStepKey(flowId, stepId);
            lock (_lock)
            {
                return _stepStatuses.GetValueOrDefault(key, GuideStepStatus.Pending);
            }
        }

        public void SetStepStatus(string flowId, string stepId, GuideStepStatus status)
        {
            if (string.IsNullOrEmpty(flowId) || string.IsNullOrEmpty(stepId)) return;

            var key = GetStepKey(flowId, stepId);
            lock (_lock)
            {
                _stepStatuses[key] = status;
                if (status == GuideStepStatus.Completed)
                {
                    _completedSteps.Add(key);
                }
            }
        }

        #endregion

        #region 当前进度

        public string GetCurrentFlowId()
        {
            lock (_lock)
            {
                return _currentFlowId;
            }
        }

        public string GetCurrentStepId()
        {
            lock (_lock)
            {
                return _currentStepId;
            }
        }

        public void SetCurrentStep(string flowId, string stepId)
        {
            lock (_lock)
            {
                _currentFlowId = flowId;
                _currentStepId = stepId;
            }
        }

        public void ClearCurrentStep()
        {
            lock (_lock)
            {
                _currentFlowId = null;
                _currentStepId = null;
            }
        }

        #endregion

        #region 完成标记

        public bool IsFlowCompleted(string flowId)
        {
            if (string.IsNullOrEmpty(flowId)) return false;

            lock (_lock)
            {
                return _completedFlows.Contains(flowId);
            }
        }

        public bool IsStepCompleted(string flowId, string stepId)
        {
            if (string.IsNullOrEmpty(flowId) || string.IsNullOrEmpty(stepId))
                return false;

            var key = GetStepKey(flowId, stepId);
            lock (_lock)
            {
                return _completedSteps.Contains(key);
            }
        }

        public void MarkFlowCompleted(string flowId)
        {
            if (string.IsNullOrEmpty(flowId)) return;

            lock (_lock)
            {
                _completedFlows.Add(flowId);
                _flowStatuses[flowId] = GuideFlowStatus.Completed;
            }
        }

        public void MarkStepCompleted(string flowId, string stepId)
        {
            if (string.IsNullOrEmpty(flowId) || string.IsNullOrEmpty(stepId)) return;

            var key = GetStepKey(flowId, stepId);
            lock (_lock)
            {
                _completedSteps.Add(key);
                _stepStatuses[key] = GuideStepStatus.Completed;
            }
        }

        #endregion

        #region 进度导入导出

        public GuideProgressData ExportProgress()
        {
            lock (_lock)
            {
                return new GuideProgressData
                {
                    completedFlows = new List<string>(_completedFlows),
                    completedSteps = new List<string>(_completedSteps),
                    currentFlowId = _currentFlowId,
                    currentStepId = _currentStepId
                };
            }
        }

        public void ImportProgress(GuideProgressData data)
        {
            if (data == null) return;

            lock (_lock)
            {
                _completedFlows.Clear();
                _completedSteps.Clear();

                if (data.completedFlows != null)
                {
                    foreach (var flowId in data.completedFlows)
                    {
                        _completedFlows.Add(flowId);
                        _flowStatuses[flowId] = GuideFlowStatus.Completed;
                    }
                }

                if (data.completedSteps != null)
                {
                    foreach (var stepKey in data.completedSteps)
                    {
                        _completedSteps.Add(stepKey);
                        _stepStatuses[stepKey] = GuideStepStatus.Completed;
                    }
                }

                _currentFlowId = data.currentFlowId;
                _currentStepId = data.currentStepId;
            }
        }

        public void ClearProgress()
        {
            lock (_lock)
            {
                _flowStatuses.Clear();
                _stepStatuses.Clear();
                _completedFlows.Clear();
                _completedSteps.Clear();
                _currentFlowId = null;
                _currentStepId = null;
            }
        }

        #endregion

        #region 私有方法

        private static string GetStepKey(string flowId, string stepId)
        {
            return $"{flowId}:{stepId}";
        }

        #endregion
    }
}

