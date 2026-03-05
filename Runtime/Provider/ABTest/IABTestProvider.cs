using System;
using System.Collections.Generic;
using JulyCore.Core;
using JulyCore.Data.ABTest;

namespace JulyCore.Provider.ABTest
{
    /// <summary>
    /// AB测试存储提供者接口
    /// 纯技术层：仅负责实验数据存储、分配记录管理
    /// 不包含分配逻辑和业务判断
    /// </summary>
    public interface IABTestProvider : IProvider
    {
        #region 实验存储（CRUD）

        /// <summary>
        /// 存储实验
        /// </summary>
        void Store(Experiment experiment);

        /// <summary>
        /// 批量存储实验
        /// </summary>
        void StoreBatch(IEnumerable<Experiment> experiments);

        /// <summary>
        /// 删除实验
        /// </summary>
        bool Remove(string experimentId);

        /// <summary>
        /// 清空所有实验
        /// </summary>
        void Clear();

        /// <summary>
        /// 更新实验
        /// </summary>
        bool Update(Experiment experiment);

        #endregion

        #region 实验查询

        /// <summary>
        /// 获取实验
        /// </summary>
        Experiment Get(string experimentId);

        /// <summary>
        /// 检查实验是否存在
        /// </summary>
        bool Exists(string experimentId);

        /// <summary>
        /// 获取所有实验
        /// </summary>
        List<Experiment> GetAll();

        /// <summary>
        /// 按状态查询实验
        /// </summary>
        List<Experiment> QueryByStatus(ExperimentStatus status);

        /// <summary>
        /// 按层级查询实验
        /// </summary>
        List<Experiment> QueryByLayer(string layer);

        /// <summary>
        /// 条件筛选查询
        /// </summary>
        List<Experiment> Query(Func<Experiment, bool> predicate);

        #endregion

        #region 分配记录管理

        /// <summary>
        /// 存储用户分配记录
        /// </summary>
        void StoreAssignment(UserExperimentAssignment assignment);

        /// <summary>
        /// 获取用户在指定实验的分配记录
        /// </summary>
        UserExperimentAssignment GetAssignment(string userId, string experimentId);

        /// <summary>
        /// 获取用户的所有分配记录
        /// </summary>
        List<UserExperimentAssignment> GetUserAssignments(string userId);

        /// <summary>
        /// 删除用户在指定实验的分配记录
        /// </summary>
        bool RemoveAssignment(string userId, string experimentId);

        /// <summary>
        /// 清空用户所有分配记录
        /// </summary>
        void ClearUserAssignments(string userId);

        /// <summary>
        /// 清空所有分配记录
        /// </summary>
        void ClearAllAssignments();

        #endregion

        #region 数据导入导出

        /// <summary>
        /// 导出用户分配数据
        /// </summary>
        ABTestSaveData Export(string userId);

        /// <summary>
        /// 导入用户分配数据
        /// </summary>
        void Import(ABTestSaveData saveData);

        #endregion
    }
}

