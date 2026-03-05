using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Core.Events;
using JulyCore.Data.ABTest;
using JulyCore.Module.Base;
using JulyCore.Provider.ABTest;

namespace JulyCore.Module.ABTest
{
    /// <summary>
    /// 条件检查处理器委托
    /// </summary>
    /// <param name="condition">条件定义</param>
    /// <param name="userId">用户ID</param>
    /// <param name="context">上下文数据</param>
    /// <returns>是否满足条件</returns>
    public delegate bool ConditionChecker(EntryCondition condition, string userId, Dictionary<string, object> context);

    /// <summary>
    /// 自定义分配处理器委托
    /// </summary>
    /// <param name="experiment">实验定义</param>
    /// <param name="userId">用户ID</param>
    /// <returns>分配的分组ID</returns>
    public delegate string CustomAllocator(Experiment experiment, string userId);

    /// <summary>
    /// AB测试模块
    /// 业务逻辑层：负责实验分配、条件检查、曝光记录
    /// </summary>
    internal class ABTestModule : ModuleBase
    {
        private IABTestProvider _provider;
        private string _currentUserId;
        private string _currentDeviceId;

        protected override LogChannel LogChannel => LogChannel.ABTest;

        // 条件检查器
        private readonly Dictionary<string, ConditionChecker> _conditionCheckers = new Dictionary<string, ConditionChecker>();

        // 自定义分配器
        private CustomAllocator _customAllocator;

        // 用户属性（用于条件检查）
        private readonly Dictionary<string, object> _userAttributes = new Dictionary<string, object>();

        private readonly object _lock = new object();

        public override int Priority => Frameworkconst.PriorityABTestModule;

        protected override UniTask OnInitAsync()
        {
            _provider = GetProvider<IABTestProvider>();
            if (_provider == null)
            {
                throw new JulyException($"[{Name}] 需要 IABTestProvider，请先注册");
            }

            // 注册默认条件检查器
            RegisterDefaultConditionCheckers();

            Log($"[{Name}] AB测试模块初始化完成");
            return base.OnInitAsync();
        }

        #region 用户设置

        internal void SetUserId(string userId)
        {
            _currentUserId = userId;
        }

        internal void SetDeviceId(string deviceId)
        {
            _currentDeviceId = deviceId;
        }

        internal void SetUserAttribute(string key, object value)
        {
            lock (_lock)
            {
                _userAttributes[key] = value;
            }
        }

        internal void SetUserAttributes(Dictionary<string, object> attributes)
        {
            if (attributes == null) return;
            lock (_lock)
            {
                foreach (var kvp in attributes)
                {
                    _userAttributes[kvp.Key] = kvp.Value;
                }
            }
        }

        internal void ClearUserAttributes()
        {
            lock (_lock)
            {
                _userAttributes.Clear();
            }
        }

        #endregion

        #region 实验管理

        internal void RegisterExperiment(Experiment experiment)
        {
            _provider.Store(experiment);
        }

        internal void RegisterExperiments(IEnumerable<Experiment> experiments)
        {
            _provider.StoreBatch(experiments);
        }

        internal void UnregisterExperiment(string experimentId)
        {
            _provider.Remove(experimentId);
        }

        internal void ClearAllExperiments()
        {
            _provider.Clear();
        }

        internal Experiment GetExperiment(string experimentId)
        {
            return _provider.Get(experimentId);
        }

        internal List<Experiment> GetAllExperiments()
        {
            return _provider.GetAll();
        }

        internal List<Experiment> GetRunningExperiments()
        {
            return _provider.QueryByStatus(ExperimentStatus.Running);
        }

        internal void SetExperimentStatus(string experimentId, ExperimentStatus status)
        {
            var experiment = _provider.Get(experimentId);
            if (experiment == null) return;

            var oldStatus = experiment.Status;
            experiment.Status = status;
            _provider.Update(experiment);

            PublishEvent(new ExperimentStatusChangedEvent
            {
                ExperimentId = experimentId,
                OldStatus = oldStatus,
                NewStatus = status,
                Experiment = experiment
            });

            Log($"[{Name}] 实验 {experimentId} 状态变更: {oldStatus} -> {status}");
        }

        #endregion

        #region 核心业务 - 分组分配

        /// <summary>
        /// 获取用户在实验中的分组（核心方法）
        /// </summary>
        internal ExperimentGroup GetUserGroup(string experimentId, string userId = null)
        {
            userId = userId ?? _currentUserId;
            if (string.IsNullOrEmpty(userId))
            {
                LogWarning($"[{Name}] 获取分组失败：用户ID为空");
                return null;
            }

            var experiment = _provider.Get(experimentId);
            if (experiment == null)
            {
                LogWarning($"[{Name}] 获取分组失败：实验 {experimentId} 不存在");
                return null;
            }

            // 检查实验是否可用
            if (!experiment.IsAvailable())
            {
                return null;
            }

            // 检查是否已分配
            var existingAssignment = _provider.GetAssignment(userId, experimentId);
            if (existingAssignment != null)
            {
                var group = experiment.Groups.Find(g => g.GroupId == existingAssignment.GroupId);
                if (group != null)
                {
                    return group;
                }
            }

            // 检查进入条件
            if (!CheckEntryConditions(experiment, userId))
            {
                return null;
            }

            // 检查互斥实验
            if (!CheckMutualExclusion(experiment, userId))
            {
                return null;
            }

            // 检查流量百分比
            if (!IsInTraffic(experiment, userId))
            {
                return null;
            }

            // 分配分组
            var assignedGroup = AllocateGroup(experiment, userId);
            if (assignedGroup == null)
            {
                return null;
            }

            // 存储分配记录
            var assignment = new UserExperimentAssignment
            {
                UserId = userId,
                ExperimentId = experimentId,
                GroupId = assignedGroup.GroupId,
                AssignedTime = DateTime.UtcNow,
                ExperimentVersion = 1
            };
            _provider.StoreAssignment(assignment);

            // 发布事件
            PublishEvent(new UserAssignedToExperimentEvent
            {
                UserId = userId,
                ExperimentId = experimentId,
                GroupId = assignedGroup.GroupId,
                Experiment = experiment,
                Group = assignedGroup,
                IsNewAssignment = true
            });

            Log($"[{Name}] 用户 {userId} 分配到实验 {experimentId} 的分组 {assignedGroup.GroupId}");
            return assignedGroup;
        }

        /// <summary>
        /// 获取实验参数值
        /// </summary>
        internal T GetParameter<T>(string experimentId, string paramKey, T defaultValue = default)
        {
            var group = GetUserGroup(experimentId);
            if (group == null)
            {
                return defaultValue;
            }

            if (group.Parameters != null && group.Parameters.TryGetValue(paramKey, out var value))
            {
                try
                {
                    if (value is T typedValue)
                    {
                        return typedValue;
                    }
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }

            return defaultValue;
        }

        /// <summary>
        /// 检查用户是否在指定分组
        /// </summary>
        internal bool IsInGroup(string experimentId, string groupId, string userId = null)
        {
            var group = GetUserGroup(experimentId, userId);
            return group != null && group.GroupId == groupId;
        }

        /// <summary>
        /// 检查用户是否在对照组
        /// </summary>
        internal bool IsInControlGroup(string experimentId, string userId = null)
        {
            var group = GetUserGroup(experimentId, userId);
            return group != null && group.IsControl;
        }

        /// <summary>
        /// 检查用户是否在实验组（非对照组）
        /// </summary>
        internal bool IsInTreatmentGroup(string experimentId, string userId = null)
        {
            var group = GetUserGroup(experimentId, userId);
            return group != null && !group.IsControl;
        }

        #endregion

        #region 曝光记录

        /// <summary>
        /// 记录实验曝光
        /// </summary>
        internal void RecordExposure(string experimentId, string scene = null,
            Dictionary<string, object> extraData = null, string userId = null)
        {
            userId = userId ?? _currentUserId;
            if (string.IsNullOrEmpty(userId)) return;

            var assignment = _provider.GetAssignment(userId, experimentId);
            if (assignment == null) return;

            bool isFirstExposure = !assignment.FirstExposureTime.HasValue;

            if (isFirstExposure)
            {
                assignment.FirstExposureTime = DateTime.UtcNow;
                _provider.StoreAssignment(assignment);
            }

            // 发布曝光事件
            PublishEvent(new ExperimentExposureEvent
            {
                UserId = userId,
                ExperimentId = experimentId,
                GroupId = assignment.GroupId,
                Scene = scene,
                IsFirstExposure = isFirstExposure,
                ExtraData = extraData
            });
        }

        #endregion

        #region 条件检查器

        internal void RegisterConditionChecker(string conditionType, ConditionChecker checker)
        {
            lock (_lock)
            {
                _conditionCheckers[conditionType] = checker;
            }
        }

        internal void SetCustomAllocator(CustomAllocator allocator)
        {
            _customAllocator = allocator;
        }

        private void RegisterDefaultConditionCheckers()
        {
            // 用户属性检查器
            RegisterConditionChecker("user_attribute", (condition, userId, context) =>
            {
                lock (_lock)
                {
                    if (!_userAttributes.TryGetValue(condition.Param, out var value))
                    {
                        return false;
                    }
                    return EvaluateCondition(value, condition.Operator, condition.Value);
                }
            });

            // 用户ID检查器
            RegisterConditionChecker("user_id", (condition, userId, context) =>
            {
                return EvaluateCondition(userId, condition.Operator, condition.Value);
            });

            // 新用户检查器
            RegisterConditionChecker("is_new_user", (condition, userId, context) =>
            {
                lock (_lock)
                {
                    if (_userAttributes.TryGetValue("is_new_user", out var value))
                    {
                        return EvaluateCondition(value, condition.Operator, condition.Value);
                    }
                    return false;
                }
            });
        }

        private bool EvaluateCondition(object actualValue, string op, object targetValue)
        {
            if (actualValue == null) return false;

            try
            {
                switch (op)
                {
                    case "eq":
                        return actualValue.ToString() == targetValue?.ToString();
                    case "ne":
                        return actualValue.ToString() != targetValue?.ToString();
                    case "gt":
                        return Convert.ToDouble(actualValue) > Convert.ToDouble(targetValue);
                    case "gte":
                        return Convert.ToDouble(actualValue) >= Convert.ToDouble(targetValue);
                    case "lt":
                        return Convert.ToDouble(actualValue) < Convert.ToDouble(targetValue);
                    case "lte":
                        return Convert.ToDouble(actualValue) <= Convert.ToDouble(targetValue);
                    case "in":
                        if (targetValue is IEnumerable<object> list)
                        {
                            foreach (var item in list)
                            {
                                if (actualValue.ToString() == item?.ToString())
                                    return true;
                            }
                        }
                        return false;
                    case "contains":
                        return actualValue.ToString().Contains(targetValue?.ToString() ?? "");
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region 分配逻辑（私有）

        private bool CheckEntryConditions(Experiment experiment, string userId)
        {
            if (experiment.EntryConditions == null || experiment.EntryConditions.Count == 0)
            {
                return true;
            }

            Dictionary<string, object> context;
            lock (_lock)
            {
                context = new Dictionary<string, object>(_userAttributes);
            }

            foreach (var condition in experiment.EntryConditions)
            {
                ConditionChecker checker;
                lock (_lock)
                {
                    if (!_conditionCheckers.TryGetValue(condition.Type, out checker))
                    {
                        LogWarning($"[{Name}] 未找到条件检查器: {condition.Type}");
                        return false;
                    }
                }

                if (!checker(condition, userId, context))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CheckMutualExclusion(Experiment experiment, string userId)
        {
            if (experiment.MutualExclusionIds == null || experiment.MutualExclusionIds.Count == 0)
            {
                return true;
            }

            foreach (var excludeId in experiment.MutualExclusionIds)
            {
                var existingAssignment = _provider.GetAssignment(userId, excludeId);
                if (existingAssignment != null)
                {
                    // 用户已在互斥实验中
                    return false;
                }
            }

            return true;
        }

        private bool IsInTraffic(Experiment experiment, string userId)
        {
            if (experiment.TrafficPercentage >= 100)
            {
                return true;
            }

            if (experiment.TrafficPercentage <= 0)
            {
                return false;
            }

            // 使用用户ID哈希确定是否在流量中
            int hash = GetDeterministicHash(userId + "_traffic_" + experiment.ExperimentId);
            int bucket = Math.Abs(hash % 100);
            return bucket < experiment.TrafficPercentage;
        }

        private ExperimentGroup AllocateGroup(Experiment experiment, string userId)
        {
            if (experiment.Groups == null || experiment.Groups.Count == 0)
            {
                return null;
            }

            // 检查白名单
            foreach (var group in experiment.Groups)
            {
                if (group.WhitelistUserIds != null && group.WhitelistUserIds.Contains(userId))
                {
                    return group;
                }
            }

            // 根据策略分配
            switch (experiment.Strategy)
            {
                case AllocationStrategy.Random:
                    return AllocateByRandom(experiment);

                case AllocationStrategy.UserIdHash:
                    return AllocateByHash(experiment, userId);

                case AllocationStrategy.DeviceIdHash:
                    return AllocateByHash(experiment, _currentDeviceId ?? userId);

                case AllocationStrategy.Custom:
                    if (_customAllocator != null)
                    {
                        var groupId = _customAllocator(experiment, userId);
                        return experiment.Groups.Find(g => g.GroupId == groupId);
                    }
                    return AllocateByHash(experiment, userId);

                default:
                    return AllocateByHash(experiment, userId);
            }
        }

        private ExperimentGroup AllocateByRandom(Experiment experiment)
        {
            int totalWeight = experiment.GetTotalWeight();
            if (totalWeight <= 0) return experiment.Groups[0];

            var random = new Random();
            int randomValue = random.Next(totalWeight);

            int cumulative = 0;
            foreach (var group in experiment.Groups)
            {
                cumulative += group.Weight;
                if (randomValue < cumulative)
                {
                    return group;
                }
            }

            return experiment.Groups[experiment.Groups.Count - 1];
        }

        private ExperimentGroup AllocateByHash(Experiment experiment, string hashKey)
        {
            int totalWeight = experiment.GetTotalWeight();
            if (totalWeight <= 0) return experiment.Groups[0];

            int hash = GetDeterministicHash(hashKey + "_" + experiment.ExperimentId);
            int bucket = Math.Abs(hash % totalWeight);

            int cumulative = 0;
            foreach (var group in experiment.Groups)
            {
                cumulative += group.Weight;
                if (bucket < cumulative)
                {
                    return group;
                }
            }

            return experiment.Groups[experiment.Groups.Count - 1];
        }

        private int GetDeterministicHash(string input)
        {
            if (string.IsNullOrEmpty(input)) return 0;

            unchecked
            {
                int hash = 5381;
                foreach (char c in input)
                {
                    hash = ((hash << 5) + hash) + c;
                }
                return hash;
            }
        }

        #endregion

        #region 数据持久化

        internal ABTestSaveData ExportData(string userId = null)
        {
            userId = userId ?? _currentUserId;
            return _provider.Export(userId);
        }

        internal void ImportData(ABTestSaveData saveData)
        {
            _provider.Import(saveData);
        }

        internal void ClearUserData(string userId = null)
        {
            userId = userId ?? _currentUserId;
            if (!string.IsNullOrEmpty(userId))
            {
                _provider.ClearUserAssignments(userId);
            }
        }

        #endregion

        #region 配置加载

        internal void LoadFromConfigTable(ExperimentConfigTable configTable)
        {
            if (configTable?.Experiments == null) return;

            var experiments = configTable.ToExperimentList();
            _provider.StoreBatch(experiments);

            Log($"[{Name}] 从配置表加载 {experiments.Count} 个实验");
        }

        #endregion

        private void PublishEvent<T>(T evt) where T : class, IEvent
        {
            try
            {
                EventBus.Publish(evt);
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 发布事件失败: {ex.Message}");
            }
        }

        protected override UniTask OnShutdownAsync()
        {
            _provider = null;
            lock (_lock)
            {
                _conditionCheckers.Clear();
                _userAttributes.Clear();
            }
            _customAllocator = null;
            Log($"[{Name}] AB测试模块已关闭");
            return base.OnShutdownAsync();
        }
    }
}

