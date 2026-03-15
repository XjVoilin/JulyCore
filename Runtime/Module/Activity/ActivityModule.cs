using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Core.Events;
using JulyCore.Data.Activity;
using JulyCore.Module.Base;
using JulyCore.Provider.Activity;

namespace JulyCore.Module.Activity
{
    /// <summary>
    /// 提供活动的通用业务能力
    /// </summary>
    internal class ActivityModule : ModuleBase
    {
        protected override LogChannel LogChannel => LogChannel.Activity;
        public override int Priority => Frameworkconst.PriorityActivityModule;

        private IActivityProvider _provider;
        private ITimeCapability _timeCapability;

        /// <summary>
        /// 已注册的活动定义
        /// </summary>
        private readonly Dictionary<string, ActivityDefinition> _definitions = new();

        /// <summary>
        /// 活动状态缓存
        /// </summary>
        private readonly Dictionary<string, ActivityState> _stateCache = new();

        /// <summary>
        /// 新开启的活动 ID 集合（本次启动后首次进入进行中状态）
        /// </summary>
        private readonly HashSet<string> _newlyOpenedIds = new();

        /// <summary>
        /// 状态检查间隔（秒）
        /// </summary>
        private const float StateCheckInterval = 60f;

        /// <summary>
        /// 上次状态检查时间
        /// </summary>
        private float _lastStateCheckTime;

        /// <summary>
        /// 是否已完成初始化
        /// </summary>
        private bool _isReady;

        protected override UniTask OnInitAsync()
        {
            _provider = GetProvider<IActivityProvider>();
            _timeCapability = GetCapability<ITimeCapability>();
            _isReady = false;
            _lastStateCheckTime = 0f;
            Log("活动模块初始化完成，等待业务层注册活动");
            return base.OnInitAsync();
        }

        protected override void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            if (!_isReady || _definitions.Count == 0)
                return;

            _lastStateCheckTime += realElapseSeconds;
            if (_lastStateCheckTime >= StateCheckInterval)
            {
                _lastStateCheckTime = 0f;
                CheckAndUpdateStates();
            }
        }

        protected override UniTask OnShutdownAsync()
        {
            _definitions.Clear();
            _stateCache.Clear();
            _newlyOpenedIds.Clear();
            _isReady = false;
            return base.OnShutdownAsync();
        }

        #region 活动注册

        /// <summary>
        /// 注册单个活动
        /// </summary>
        internal void RegisterActivity(ActivityDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.Id))
            {
                LogWarning("注册活动失败：活动定义无效");
                return;
            }

            if (_definitions.ContainsKey(definition.Id))
            {
                Log($"活动已存在，将覆盖: {definition.Id}");
            }

            _definitions[definition.Id] = definition;

            // 计算并缓存状态
            var state = CalculateState(definition);
            _stateCache[definition.Id] = state;

            Log($"注册活动: {definition.Id}, Type: {definition.Type}, State: {state}");
        }

        /// <summary>
        /// 批量注册活动
        /// </summary>
        internal void RegisterActivities(IEnumerable<ActivityDefinition> definitions)
        {
            if (definitions == null)
                return;

            foreach (var def in definitions)
            {
                RegisterActivity(def);
            }

            Log($"批量注册活动完成，当前共 {_definitions.Count} 个活动");
        }

        /// <summary>
        /// 注销活动
        /// </summary>
        internal bool UnregisterActivity(string activityId)
        {
            if (string.IsNullOrEmpty(activityId))
                return false;

            var removed = _definitions.Remove(activityId);
            _stateCache.Remove(activityId);
            _newlyOpenedIds.Remove(activityId);

            if (removed)
            {
                Log($"注销活动: {activityId}");
            }

            return removed;
        }

        /// <summary>
        /// 完成注册并初始化状态
        /// 业务层注册完所有活动后调用
        /// </summary>
        internal void CompleteRegistration()
        {
            if (_isReady)
            {
                LogWarning("活动模块已就绪，请勿重复调用 CompleteRegistration");
                return;
            }

            // 处理活动状态
            ProcessActivityStates();

            _isReady = true;

            // 发布注册完成事件
            EventBus.Publish(new ActivityRegisteredEvent
            {
                RegisteredCount = _definitions.Count
            });

            // 发布模块就绪事件
            var activeCount = _stateCache.Values.Count(s => s == ActivityState.InProgress);
            EventBus.Publish(new ActivityModuleReadyEvent
            {
                ActiveCount = activeCount,
                NewlyOpenedCount = _newlyOpenedIds.Count
            });

            Log($"活动模块就绪，共 {_definitions.Count} 个活动，进行中 {activeCount} 个，新开启 {_newlyOpenedIds.Count} 个");
        }

        #endregion

        #region 活动查询

        /// <summary>
        /// 获取所有活动
        /// </summary>
        internal List<ActivityInfo> GetAllActivities()
        {
            var result = new List<ActivityInfo>();
            foreach (var kvp in _definitions)
            {
                var info = BuildActivityInfo(kvp.Key, kvp.Value);
                result.Add(info);
            }

            return result.OrderBy(a => a.Definition.Priority).ToList();
        }

        /// <summary>
        /// 获取指定活动
        /// </summary>
        internal ActivityInfo GetActivity(string activityId)
        {
            if (string.IsNullOrEmpty(activityId))
                return null;

            if (!_definitions.TryGetValue(activityId, out var def))
                return null;

            return BuildActivityInfo(activityId, def);
        }

        /// <summary>
        /// 按类型获取活动
        /// </summary>
        internal List<ActivityInfo> GetActivitiesByType(int type)
        {
            return GetAllActivities().Where(a => a.Definition.Type == type).ToList();
        }

        /// <summary>
        /// 按状态获取活动
        /// </summary>
        internal List<ActivityInfo> GetActivitiesByState(ActivityState state)
        {
            return GetAllActivities().Where(a => a.State == state).ToList();
        }

        /// <summary>
        /// 获取活动状态
        /// </summary>
        internal ActivityState GetActivityState(string activityId)
        {
            if (string.IsNullOrEmpty(activityId))
                return ActivityState.NotStarted;

            if (_stateCache.TryGetValue(activityId, out var state))
                return state;

            if (_definitions.TryGetValue(activityId, out var def))
            {
                state = CalculateState(def);
                _stateCache[activityId] = state;
                return state;
            }

            return ActivityState.NotStarted;
        }

        /// <summary>
        /// 检查活动是否存在
        /// </summary>
        internal bool HasActivity(string activityId)
        {
            return !string.IsNullOrEmpty(activityId) && _definitions.ContainsKey(activityId);
        }

        #endregion

        #region 活动记录

        /// <summary>
        /// 获取活动运行时记录
        /// </summary>
        internal ActivityRecord GetActivityRecord(string activityId)
        {
            return _provider.GetActivityRecord(activityId);
        }

        /// <summary>
        /// 保存活动进度数据
        /// </summary>
        internal void SaveProgressData(string activityId, string dataPayload)
        {
            if (string.IsNullOrEmpty(activityId))
                return;

            var record = _provider.GetOrCreateActivityRecord(activityId);
            record.DataPayload = dataPayload;
            record.LastUpdateTime = _timeCapability.ServerTimeSeconds;

            _provider.SaveActivityRecord(record);

            // 发布进度变更事件
            EventBus.Publish(new ActivityProgressChangedEvent
            {
                ActivityId = activityId,
                Record = record
            });

            Log($"保存活动进度: {activityId}");
        }

        #endregion

        #region 数据管理

        /// <summary>
        /// 清理活动数据
        /// </summary>
        internal void ClearActivityData(string activityId)
        {
            _provider.ClearActivityData(activityId);
            Log($"清理活动数据: {activityId}");
        }

        #endregion

        #region 状态计算

        /// <summary>
        /// 计算活动状态
        /// </summary>
        public ActivityState CalculateState(long preAnnounceTime, long startTime, long endTime)
        {
            var now = _timeCapability.ServerTimeSeconds;

            if (now > endTime)
                return ActivityState.Ended;

            if (now >= startTime)
                return ActivityState.InProgress;

            if (preAnnounceTime > 0 && preAnnounceTime < startTime && now >= preAnnounceTime)
                return ActivityState.PreAnnounce;

            return ActivityState.NotStarted;
        }

        /// <summary>
        /// 计算活动状态（无预告期）
        /// </summary>
        public ActivityState CalculateState(long startTime, long endTime)
        {
            return CalculateState(0, startTime, endTime);
        }

        /// <summary>
        /// 计算活动状态
        /// </summary>
        private ActivityState CalculateState(ActivityDefinition definition)
        {
            return CalculateState(definition.PreAnnounceTime, definition.StartTime, definition.EndTime);
        }

        /// <summary>
        /// 检查并更新所有活动状态
        /// </summary>
        private void CheckAndUpdateStates()
        {
            foreach (var kvp in _definitions)
            {
                var activityId = kvp.Key;
                var definition = kvp.Value;
                var oldState = _stateCache.GetValueOrDefault(activityId, ActivityState.NotStarted);
                var newState = CalculateState(definition);

                if (oldState != newState)
                {
                    _stateCache[activityId] = newState;
                    OnActivityStateChanged(activityId, definition, oldState, newState);
                }
            }
        }

        /// <summary>
        /// 处理活动状态
        /// </summary>
        private void ProcessActivityStates()
        {
            foreach (var kvp in _definitions)
            {
                var activityId = kvp.Key;
                var definition = kvp.Value;
                var state = _stateCache.GetValueOrDefault(activityId, ActivityState.NotStarted);

                // 检查是否为新开启的活动
                if (state == ActivityState.InProgress)
                {
                    var wasOpened = _provider.IsActivityOpened(activityId);
                    if (!wasOpened)
                    {
                        // 标记为新开启
                        _newlyOpenedIds.Add(activityId);
                        _provider.MarkActivityOpened(activityId);

                        // 发布开启事件
                        EventBus.Publish(new ActivityOpenedEvent
                        {
                            ActivityId = activityId,
                            Definition = definition
                        });

                        Log($"活动首次开启: {activityId}");
                    }
                }
            }
        }

        /// <summary>
        /// 活动状态变更处理
        /// </summary>
        private void OnActivityStateChanged(string activityId, ActivityDefinition definition,
            ActivityState oldState, ActivityState newState)
        {
            Log($"活动状态变更: {activityId}, {oldState} -> {newState}");

            // 进入进行中状态（从未开始或预告期进入）
            if (newState == ActivityState.InProgress && 
                (oldState == ActivityState.NotStarted || oldState == ActivityState.PreAnnounce))
            {
                var wasOpened = _provider.IsActivityOpened(activityId);
                if (!wasOpened)
                {
                    _newlyOpenedIds.Add(activityId);
                    _provider.MarkActivityOpened(activityId);
                }

                EventBus.Publish(new ActivityOpenedEvent
                {
                    ActivityId = activityId,
                    Definition = definition
                });
            }
            // 进入已结束状态
            else if (newState == ActivityState.Ended)
            {
                _newlyOpenedIds.Remove(activityId);

                EventBus.Publish(new ActivityClosedEvent
                {
                    ActivityId = activityId,
                    Definition = definition
                });
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 构建活动信息
        /// </summary>
        private ActivityInfo BuildActivityInfo(string activityId, ActivityDefinition definition)
        {
            return new ActivityInfo
            {
                Definition = definition,
                State = GetActivityState(activityId),
                Record = _provider.GetActivityRecord(activityId),
                IsNewlyOpened = _newlyOpenedIds.Contains(activityId)
            };
        }

        #endregion
    }
}