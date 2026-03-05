using System.Collections.Generic;
using JulyCore.Data.RedDot;

namespace JulyCore.Core.Events
{
    /// <summary>
    /// 红点变更事件
    /// </summary>
    public class RedDotChangedEvent : IEvent
    {
        public string Key { get; set; }
        public int OldCount { get; set; }
        public int NewCount { get; set; }
        public RedDotType Type { get; set; }
        public bool JustAppeared => OldCount == 0 && NewCount > 0;
        public bool JustDisappeared => OldCount > 0 && NewCount == 0;
    }

    /// <summary>
    /// 红点批量变更事件
    /// </summary>
    public class RedDotBatchChangedEvent : IEvent
    {
        public List<RedDotChangeInfo> Changes { get; set; } = new List<RedDotChangeInfo>();
    }

    /// <summary>
    /// 红点启用状态变更事件
    /// </summary>
    public class RedDotEnabledChangedEvent : IEvent
    {
        /// <summary>
        /// 变更的节点 Key（null 表示全局变更）
        /// </summary>
        public string Key { get; set; }
        
        /// <summary>
        /// 是否启用
        /// </summary>
        public bool Enabled { get; set; }
        
        /// <summary>
        /// 是否是全局变更
        /// </summary>
        public bool IsGlobal => Key == null;
    }
}

