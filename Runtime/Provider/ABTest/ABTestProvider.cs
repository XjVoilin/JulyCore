using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Data.ABTest;
using JulyCore.Provider.Base;

namespace JulyCore.Provider.ABTest
{
    /// <summary>
    /// AB测试存储提供者实现
    /// 纯技术层：仅负责实验数据存储、分配记录管理
    /// </summary>
    internal class ABTestProvider : ProviderBase, IABTestProvider
    {
        public override int Priority => Frameworkconst.PriorityABTestProvider;
        protected override LogChannel LogChannel => LogChannel.ABTest;

        // 实验存储：ExperimentId -> Experiment
        private readonly Dictionary<string, Experiment> _experiments = new Dictionary<string, Experiment>();

        // 用户分配记录：UserId -> (ExperimentId -> Assignment)
        private readonly Dictionary<string, Dictionary<string, UserExperimentAssignment>> _assignments
            = new Dictionary<string, Dictionary<string, UserExperimentAssignment>>();

        private readonly object _lock = new object();

        protected override UniTask OnInitAsync()
        {
            return UniTask.CompletedTask;
        }

        protected override void OnShutdown()
        {
            lock (_lock)
            {
                _experiments.Clear();
                _assignments.Clear();
            }
        }

        #region 实验存储（CRUD）

        public void Store(Experiment experiment)
        {
            if (experiment == null || string.IsNullOrEmpty(experiment.ExperimentId))
            {
                LogWarning($"[{Name}] 存储失败：实验数据或ID为空");
                return;
            }

            lock (_lock)
            {
                _experiments[experiment.ExperimentId] = experiment;
            }
        }

        public void StoreBatch(IEnumerable<Experiment> experiments)
        {
            if (experiments == null) return;

            lock (_lock)
            {
                foreach (var experiment in experiments)
                {
                    if (experiment != null && !string.IsNullOrEmpty(experiment.ExperimentId))
                    {
                        _experiments[experiment.ExperimentId] = experiment;
                    }
                }
            }
        }

        public bool Remove(string experimentId)
        {
            if (string.IsNullOrEmpty(experimentId)) return false;

            lock (_lock)
            {
                return _experiments.Remove(experimentId);
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _experiments.Clear();
            }
        }

        public bool Update(Experiment experiment)
        {
            if (experiment == null || string.IsNullOrEmpty(experiment.ExperimentId))
                return false;

            lock (_lock)
            {
                if (!_experiments.ContainsKey(experiment.ExperimentId))
                    return false;

                _experiments[experiment.ExperimentId] = experiment;
                return true;
            }
        }

        #endregion

        #region 实验查询

        public Experiment Get(string experimentId)
        {
            if (string.IsNullOrEmpty(experimentId)) return null;

            lock (_lock)
            {
                return _experiments.TryGetValue(experimentId, out var experiment) ? experiment : null;
            }
        }

        public bool Exists(string experimentId)
        {
            if (string.IsNullOrEmpty(experimentId)) return false;

            lock (_lock)
            {
                return _experiments.ContainsKey(experimentId);
            }
        }

        public List<Experiment> GetAll()
        {
            lock (_lock)
            {
                return _experiments.Values.ToList();
            }
        }

        public List<Experiment> QueryByStatus(ExperimentStatus status)
        {
            lock (_lock)
            {
                return _experiments.Values.Where(e => e.Status == status).ToList();
            }
        }

        public List<Experiment> QueryByLayer(string layer)
        {
            lock (_lock)
            {
                return _experiments.Values.Where(e => e.Layer == layer).ToList();
            }
        }

        public List<Experiment> Query(Func<Experiment, bool> predicate)
        {
            if (predicate == null) return GetAll();

            lock (_lock)
            {
                return _experiments.Values.Where(predicate).ToList();
            }
        }

        #endregion

        #region 分配记录管理

        public void StoreAssignment(UserExperimentAssignment assignment)
        {
            if (assignment == null ||
                string.IsNullOrEmpty(assignment.UserId) ||
                string.IsNullOrEmpty(assignment.ExperimentId))
            {
                return;
            }

            lock (_lock)
            {
                if (!_assignments.TryGetValue(assignment.UserId, out var userAssignments))
                {
                    userAssignments = new Dictionary<string, UserExperimentAssignment>();
                    _assignments[assignment.UserId] = userAssignments;
                }

                userAssignments[assignment.ExperimentId] = assignment;
            }
        }

        public UserExperimentAssignment GetAssignment(string userId, string experimentId)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(experimentId))
                return null;

            lock (_lock)
            {
                if (_assignments.TryGetValue(userId, out var userAssignments))
                {
                    if (userAssignments.TryGetValue(experimentId, out var assignment))
                    {
                        return assignment;
                    }
                }
                return null;
            }
        }

        public List<UserExperimentAssignment> GetUserAssignments(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return new List<UserExperimentAssignment>();

            lock (_lock)
            {
                if (_assignments.TryGetValue(userId, out var userAssignments))
                {
                    return userAssignments.Values.ToList();
                }
                return new List<UserExperimentAssignment>();
            }
        }

        public bool RemoveAssignment(string userId, string experimentId)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(experimentId))
                return false;

            lock (_lock)
            {
                if (_assignments.TryGetValue(userId, out var userAssignments))
                {
                    return userAssignments.Remove(experimentId);
                }
                return false;
            }
        }

        public void ClearUserAssignments(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return;

            lock (_lock)
            {
                _assignments.Remove(userId);
            }
        }

        public void ClearAllAssignments()
        {
            lock (_lock)
            {
                _assignments.Clear();
            }
        }

        #endregion

        #region 数据导入导出

        public ABTestSaveData Export(string userId)
        {
            var saveData = new ABTestSaveData
            {
                UserId = userId,
                Assignments = new Dictionary<string, UserExperimentAssignment>()
            };

            lock (_lock)
            {
                if (_assignments.TryGetValue(userId, out var userAssignments))
                {
                    foreach (var kvp in userAssignments)
                    {
                        saveData.Assignments[kvp.Key] = kvp.Value;
                    }
                }
            }

            return saveData;
        }

        public void Import(ABTestSaveData saveData)
        {
            if (saveData == null || string.IsNullOrEmpty(saveData.UserId))
                return;

            lock (_lock)
            {
                if (!_assignments.TryGetValue(saveData.UserId, out var userAssignments))
                {
                    userAssignments = new Dictionary<string, UserExperimentAssignment>();
                    _assignments[saveData.UserId] = userAssignments;
                }

                if (saveData.Assignments != null)
                {
                    foreach (var kvp in saveData.Assignments)
                    {
                        userAssignments[kvp.Key] = kvp.Value;
                    }
                }
            }
        }

        #endregion
    }
}

