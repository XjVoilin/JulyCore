using JulyCore.Data.Activity;

namespace JulyCore.Provider.Activity
{
    /// <summary>
    /// 活动数据提供者
    /// 仅负责活动记录的读写与持久化，不包含任何业务逻辑
    /// </summary>
    public interface IActivityProvider : Core.IProvider
    {
        #region 活动记录操作

        /// <summary>
        /// 获取指定活动的运行时记录
        /// </summary>
        /// <param name="activityId">活动 ID</param>
        /// <returns>活动记录，不存在则返回 null</returns>
        ActivityRecord GetActivityRecord(string activityId);

        /// <summary>
        /// 获取或创建指定活动的运行时记录
        /// </summary>
        /// <param name="activityId">活动 ID</param>
        /// <returns>活动记录（不存在则创建新的）</returns>
        ActivityRecord GetOrCreateActivityRecord(string activityId);

        /// <summary>
        /// 保存指定活动的运行时记录
        /// </summary>
        /// <param name="record">活动记录</param>
        void SaveActivityRecord(ActivityRecord record);

        #endregion

        #region 活动开启状态

        /// <summary>
        /// 检查活动是否已开启过
        /// </summary>
        /// <param name="activityId">活动 ID</param>
        /// <returns>是否已开启过</returns>
        bool IsActivityOpened(string activityId);

        /// <summary>
        /// 标记活动已开启
        /// </summary>
        /// <param name="activityId">活动 ID</param>
        void MarkActivityOpened(string activityId);

        #endregion

        #region 数据清理

        /// <summary>
        /// 清理指定活动的数据
        /// Provider 内部负责持久化
        /// </summary>
        /// <param name="activityId">活动 ID</param>
        void ClearActivityData(string activityId);

        #endregion
    }
}
