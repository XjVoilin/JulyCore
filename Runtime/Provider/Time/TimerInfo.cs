using System;

namespace JulyCore.Provider.Time
{
    /// <summary>
    /// 定时器信息
    /// </summary>
    internal class TimerInfo
    {
        /// <summary>
        /// 定时器ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 间隔时间（秒）
        /// </summary>
        public float Interval { get; set; }

        /// <summary>
        /// 剩余时间（秒）
        /// </summary>
        public float RemainingTime { get; set; }

        /// <summary>
        /// 回调函数
        /// </summary>
        public Action Callback { get; set; }

        /// <summary>
        /// 是否使用真实时间
        /// </summary>
        public bool UseRealTime { get; set; }

        /// <summary>
        /// 是否重复执行
        /// </summary>
        public bool IsRepeat { get; set; }

        /// <summary>
        /// 剩余重复次数（-1表示无限）
        /// </summary>
        public int RemainingRepeatCount { get; set; }

        /// <summary>
        /// 是否暂停
        /// </summary>
        public bool IsPaused { get; set; }

        /// <summary>
        /// 是否已取消
        /// </summary>
        public bool IsCancelled { get; set; }
    }
}

