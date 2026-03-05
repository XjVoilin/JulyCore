using System;
using System.Collections.Generic;
using JulyCore.Core;
using JulyCore.Data.Task;

namespace JulyCore.Provider.Task
{
    /// <summary>
    /// 任务存储提供者接口
    /// 纯技术层：仅负责数据存储、索引维护、CRUD操作
    /// 不包含任何业务逻辑判断（如解锁条件检测、状态流转规则）
    /// 
    /// 用户可实现此接口来自定义存储方式（如：服务器同步、数据库存储等）
    /// </summary>
    public interface ITaskProvider : IProvider
    {
        #region 数据存储（CRUD）

        /// <summary>
        /// 存储任务数据
        /// </summary>
        /// <param name="taskData">任务数据</param>
        void Store(TaskData taskData);

        /// <summary>
        /// 批量存储任务数据
        /// </summary>
        /// <param name="tasks">任务数据列表</param>
        void StoreBatch(IEnumerable<TaskData> tasks);

        /// <summary>
        /// 删除任务数据
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <returns>是否删除成功</returns>
        bool Remove(string taskId);

        /// <summary>
        /// 清空所有任务数据
        /// </summary>
        void Clear();

        /// <summary>
        /// 更新任务数据
        /// </summary>
        /// <param name="taskData">任务数据</param>
        /// <returns>是否更新成功</returns>
        bool Update(TaskData taskData);

        #endregion

        #region 数据查询

        /// <summary>
        /// 获取任务数据
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <returns>任务数据，不存在返回null</returns>
        TaskData Get(string taskId);

        /// <summary>
        /// 尝试获取任务数据
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <param name="taskData">输出的任务数据</param>
        /// <returns>是否存在</returns>
        bool TryGet(string taskId, out TaskData taskData);

        /// <summary>
        /// 检查任务是否存在
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <returns>是否存在</returns>
        bool Exists(string taskId);

        /// <summary>
        /// 获取所有任务数据
        /// </summary>
        /// <returns>任务列表</returns>
        List<TaskData> GetAll();

        /// <summary>
        /// 获取任务总数
        /// </summary>
        /// <returns>任务数量</returns>
        int Count();

        #endregion

        #region 索引查询

        /// <summary>
        /// 按类型查询任务
        /// </summary>
        /// <param name="type">任务类型</param>
        /// <returns>任务列表</returns>
        List<TaskData> QueryByType(TaskType type);

        /// <summary>
        /// 按状态查询任务
        /// </summary>
        /// <param name="state">任务状态</param>
        /// <returns>任务列表</returns>
        List<TaskData> QueryByState(TaskState state);

        /// <summary>
        /// 按分组查询任务
        /// </summary>
        /// <param name="group">任务分组</param>
        /// <returns>任务列表</returns>
        List<TaskData> QueryByGroup(string group);

        /// <summary>
        /// 按条件类型和参数查询包含该条件的任务
        /// </summary>
        /// <param name="conditionType">条件类型</param>
        /// <param name="param">条件参数</param>
        /// <returns>任务ID和条件ID的列表</returns>
        List<(string TaskId, string ConditionId)> QueryByCondition(TaskConditionType conditionType, string param);

        /// <summary>
        /// 条件筛选查询
        /// </summary>
        /// <param name="predicate">筛选条件</param>
        /// <returns>任务列表</returns>
        List<TaskData> Query(Func<TaskData, bool> predicate);

        #endregion

        #region 数据导入导出

        /// <summary>
        /// 导出所有任务数据（用于存档）
        /// </summary>
        /// <returns>任务ID -> 存档数据</returns>
        Dictionary<string, TaskSaveData> Export();

        /// <summary>
        /// 导入任务进度数据（用于读档）
        /// </summary>
        /// <param name="saveData">存档数据</param>
        void Import(Dictionary<string, TaskSaveData> saveData);

        #endregion
    }
}
