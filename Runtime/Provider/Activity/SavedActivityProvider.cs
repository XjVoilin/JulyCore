using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Data.Activity;
using JulyCore.Provider.Base;
using JulyCore.Provider.Save;

namespace JulyCore.Provider.Activity
{
    /// <summary>
    /// 基于本地存档系统实现活动运行时的事实数据
    /// </summary>
    public class SavedActivityProvider : ProviderBase, IActivityProvider
    {
        public override int Priority => Frameworkconst.PriorityActivityProvider;
        protected override LogChannel LogChannel => LogChannel.Activity;

        private readonly ISaveProvider _saveProvider;
        private ActivityRuntimeData _runtimeData;

        public SavedActivityProvider(ISaveProvider saveProvider)
        {
            _saveProvider = saveProvider;
        }

        #region 生命周期

        protected override async UniTask OnInitAsync()
        {
            _runtimeData = await _saveProvider.LoadAndRegisterAsync<ActivityRuntimeData>(
                Frameworkconst.ActivitySaveKey);
        }

        protected override void OnShutdown()
        {
            _runtimeData = null;
        }

        #endregion

        #region IActivityProvider 实现

        public ActivityRecord GetActivityRecord(string activityId)
        {
            if (_runtimeData == null || string.IsNullOrEmpty(activityId))
                return null;

            _runtimeData.RecordMap.TryGetValue(activityId, out var record);
            return record;
        }

        public ActivityRecord GetOrCreateActivityRecord(string activityId)
        {
            if (_runtimeData == null || string.IsNullOrEmpty(activityId))
                return null;

            if (!_runtimeData.RecordMap.TryGetValue(activityId, out var record))
            {
                record = new ActivityRecord { ActivityId = activityId };
                _runtimeData.RecordMap[activityId] = record;
            }

            return record;
        }

        public void SaveActivityRecord(ActivityRecord record)
        {
            if (_runtimeData == null || record == null || string.IsNullOrEmpty(record.ActivityId))
                return;

            _runtimeData.RecordMap[record.ActivityId] = record;
            MarkDirtyInternal();
        }

        public bool IsActivityOpened(string activityId)
        {
            if (_runtimeData == null || string.IsNullOrEmpty(activityId))
                return false;

            return _runtimeData.OpenedActivityIds.Contains(activityId);
        }

        public void MarkActivityOpened(string activityId)
        {
            if (_runtimeData == null || string.IsNullOrEmpty(activityId))
                return;

            if (_runtimeData.OpenedActivityIds.Add(activityId))
            {
                MarkDirtyInternal();
            }
        }

        public void ClearActivityData(string activityId)
        {
            if (_runtimeData == null || string.IsNullOrEmpty(activityId))
                return;

            var changed = _runtimeData.RecordMap.Remove(activityId);

            if (_runtimeData.OpenedActivityIds.Remove(activityId))
                changed = true;

            if (changed)
            {
                MarkDirtyInternal();
            }
        }

        #endregion

        #region 内部方法

        /// <summary>
        /// 标记数据为脏
        /// </summary>
        private void MarkDirtyInternal()
        {
            _saveProvider.MarkDirty(Frameworkconst.ActivitySaveKey);
        }

        #endregion
    }
}