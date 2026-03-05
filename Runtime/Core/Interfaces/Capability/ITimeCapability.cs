using System;

namespace JulyCore.Core
{
    /// <summary>
    /// 时间能力接口
    /// 供其他 Module 使用定时器功能
    /// </summary>
    public interface ITimeCapability : ICapability
    {
        /// <summary>
        /// 服务器时间（UTC）
        /// 如果未同步则返回本地时间
        /// </summary>
        DateTime ServerTimeUtc { get; }
        
        /// <summary>
        /// 服务器时间戳
        /// </summary>
        long ServerTimeSeconds { get; }
        
        /// <summary>
        /// 注册一次性定时器
        /// </summary>
        /// <param name="delay">延迟时间（秒）</param>
        /// <param name="callback">回调函数</param>
        /// <param name="useRealTime">是否使用真实时间</param>
        /// <returns>定时器 ID</returns>
        int ScheduleOnce(float delay, Action callback, bool useRealTime = false);

        /// <summary>
        /// 注册重复定时器
        /// </summary>
        /// <param name="interval">间隔时间（秒）</param>
        /// <param name="callback">回调函数</param>
        /// <param name="useRealTime">是否使用真实时间</param>
        /// <param name="repeatCount">重复次数，-1 表示无限</param>
        /// <returns>定时器 ID</returns>
        int ScheduleRepeat(float interval, Action callback, bool useRealTime = false, int repeatCount = -1);

        /// <summary>
        /// 取消定时器
        /// </summary>
        /// <param name="timerId">定时器 ID</param>
        /// <returns>是否取消成功</returns>
        bool CancelTimer(int timerId);
    }
}
