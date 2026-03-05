using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Data.Task;
using JulyCore.Provider.Base;

namespace JulyCore.Provider.Task
{
    /// <summary>
    /// 任务存储提供者实现
    /// 纯技术层：仅负责数据存储、索引维护、CRUD操作
    /// 不包含任何业务逻辑判断
    /// </summary>
    internal class TaskProvider : ProviderBase, ITaskProvider
    {
        public override int Priority => Frameworkconst.PriorityTaskProvider;
        protected override LogChannel LogChannel => LogChannel.Task;

        // 主存储：TaskId -> TaskData
        private readonly Dictionary<string, TaskData> _storage = new();

        // 条件索引：(ConditionType, Param) -> List<(TaskId, ConditionId)>
        private readonly Dictionary<(TaskConditionType, string), List<(string TaskId, string ConditionId)>> _conditionIndex
            = new();

        private readonly object _lock = new object();

        protected override UniTask OnInitAsync()
        {
            Log($"[{Name}] 任务存储提供者初始化完成");
            return UniTask.CompletedTask;
        }

        protected override UniTask OnShutdownAsync()
        {
            lock (_lock)
            {
                _storage.Clear();
                _conditionIndex.Clear();
            }
            Log($"[{Name}] 任务存储提供者已关闭");
            return UniTask.CompletedTask;
        }

        #region 数据存储（CRUD）

        public void Store(TaskData taskData)
        {
            if (taskData == null || string.IsNullOrEmpty(taskData.TaskId))
            {
                LogWarning($"[{Name}] 存储失败：任务数据或ID为空");
                return;
            }

            lock (_lock)
            {
                // 如果已存在，先移除旧索引
                if (_storage.ContainsKey(taskData.TaskId))
                {
                    RemoveFromIndex(taskData.TaskId);
                }

                _storage[taskData.TaskId] = taskData;
                AddToIndex(taskData);
            }
        }

        public void StoreBatch(IEnumerable<TaskData> tasks)
        {
            if (tasks == null) return;

            foreach (var task in tasks)
            {
                Store(task);
            }
        }

        public bool Remove(string taskId)
        {
            if (string.IsNullOrEmpty(taskId)) return false;

            lock (_lock)
            {
                if (_storage.Remove(taskId))
                {
                    RemoveFromIndex(taskId);
                    return true;
                }
                return false;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _storage.Clear();
                _conditionIndex.Clear();
            }
        }

        public bool Update(TaskData taskData)
        {
            if (taskData == null || string.IsNullOrEmpty(taskData.TaskId))
                return false;

            lock (_lock)
            {
                if (!_storage.ContainsKey(taskData.TaskId))
                    return false;

                // 更新索引
                RemoveFromIndex(taskData.TaskId);
                _storage[taskData.TaskId] = taskData;
                AddToIndex(taskData);
                return true;
            }
        }

        #endregion

        #region 数据查询

        public TaskData Get(string taskId)
        {
            if (string.IsNullOrEmpty(taskId)) return null;

            lock (_lock)
            {
                return _storage.GetValueOrDefault(taskId);
            }
        }

        public bool TryGet(string taskId, out TaskData taskData)
        {
            taskData = null;
            if (string.IsNullOrEmpty(taskId)) return false;

            lock (_lock)
            {
                return _storage.TryGetValue(taskId, out taskData);
            }
        }

        public bool Exists(string taskId)
        {
            if (string.IsNullOrEmpty(taskId)) return false;

            lock (_lock)
            {
                return _storage.ContainsKey(taskId);
            }
        }

        public List<TaskData> GetAll()
        {
            lock (_lock)
            {
                return _storage.Values.ToList();
            }
        }

        public int Count()
        {
            lock (_lock)
            {
                return _storage.Count;
            }
        }

        #endregion

        #region 索引查询

        public List<TaskData> QueryByType(TaskType type)
        {
            lock (_lock)
            {
                return _storage.Values.Where(t => t.Type == type).ToList();
            }
        }

        public List<TaskData> QueryByState(TaskState state)
        {
            lock (_lock)
            {
                return _storage.Values.Where(t => t.State == state).ToList();
            }
        }

        public List<TaskData> QueryByGroup(string group)
        {
            lock (_lock)
            {
                return _storage.Values.Where(t => t.Group == group).ToList();
            }
        }

        public List<(string TaskId, string ConditionId)> QueryByCondition(TaskConditionType conditionType, string param)
        {
            lock (_lock)
            {
                var key = (conditionType, param ?? string.Empty);
                if (_conditionIndex.TryGetValue(key, out var list))
                {
                    return new List<(string, string)>(list);
                }
                return new List<(string, string)>();
            }
        }

        public List<TaskData> Query(Func<TaskData, bool> predicate)
        {
            if (predicate == null) return GetAll();

            lock (_lock)
            {
                return _storage.Values.Where(predicate).ToList();
            }
        }

        #endregion

        #region 数据导入导出

        public Dictionary<string, TaskSaveData> Export()
        {
            var result = new Dictionary<string, TaskSaveData>();

            lock (_lock)
            {
                foreach (var kvp in _storage)
                {
                    var task = kvp.Value;
                    var saveData = new TaskSaveData
                    {
                        State = task.State,
                        ConditionProgress = new Dictionary<string, int>()
                    };

                    if (task.Conditions != null)
                    {
                        foreach (var condition in task.Conditions)
                        {
                            saveData.ConditionProgress[condition.ConditionId] = condition.CurrentValue;
                        }
                    }

                    result[kvp.Key] = saveData;
                }
            }

            return result;
        }

        public void Import(Dictionary<string, TaskSaveData> saveData)
        {
            if (saveData == null) return;

            lock (_lock)
            {
                foreach (var kvp in saveData)
                {
                    if (!_storage.TryGetValue(kvp.Key, out var task))
                        continue;

                    var data = kvp.Value;
                    task.State = data.State;

                    if (task.Conditions != null && data.ConditionProgress != null)
                    {
                        foreach (var condition in task.Conditions)
                        {
                            if (data.ConditionProgress.TryGetValue(condition.ConditionId, out var value))
                            {
                                condition.CurrentValue = value;
                            }
                        }
                    }
                }
            }

            Log($"[{Name}] 导入任务数据完成，共 {saveData.Count} 条");
        }

        #endregion

        #region 私有方法 - 索引维护

        private void AddToIndex(TaskData task)
        {
            if (task.Conditions == null) return;

            foreach (var condition in task.Conditions)
            {
                var key = (condition.Type, condition.Param ?? string.Empty);
                if (!_conditionIndex.TryGetValue(key, out var list))
                {
                    list = new List<(string, string)>();
                    _conditionIndex[key] = list;
                }
                list.Add((task.TaskId, condition.ConditionId));
            }
        }

        private void RemoveFromIndex(string taskId)
        {
            var keysToCheck = _conditionIndex.Keys;
            foreach (var key in keysToCheck)
            {
                var list = _conditionIndex[key];
                list.RemoveAll(item => item.TaskId == taskId);
                if (list.Count == 0)
                {
                    _conditionIndex.Remove(key);
                }
            }
        }

        #endregion
    }
}
