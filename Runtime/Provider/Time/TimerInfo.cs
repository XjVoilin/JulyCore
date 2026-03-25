using System;
using System.Collections.Generic;

namespace JulyCore.Provider.Time
{
    /// <summary>
    /// 定时器信息（池化复用，避免高频 new 产生 GC）
    /// </summary>
    internal class TimerInfo
    {
        private static readonly Stack<TimerInfo> Pool = new(32);

        public int Id;
        public float Interval;
        public float RemainingTime;
        public Action Callback;
        public bool UseRealTime;
        public bool IsRepeat;
        public int RemainingRepeatCount;
        public bool IsPaused;
        public bool IsCancelled;

        public static TimerInfo Rent()
        {
            return Pool.Count > 0 ? Pool.Pop() : new TimerInfo();
        }

        public static void Return(TimerInfo info)
        {
            if (info == null) return;
            info.Id = 0;
            info.Interval = 0f;
            info.RemainingTime = 0f;
            info.Callback = null;
            info.UseRealTime = false;
            info.IsRepeat = false;
            info.RemainingRepeatCount = 0;
            info.IsPaused = false;
            info.IsCancelled = false;
            Pool.Push(info);
        }

        public static void ClearPool() => Pool.Clear();
    }
}

