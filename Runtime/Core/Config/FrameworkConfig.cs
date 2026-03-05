using System;
using JulyCore.Module.Task;
using JulyCore.Provider.Resource;
using UnityEngine;

namespace JulyCore.Core.Config
{
    /// <summary>
    /// 框架配置 ScriptableObject
    /// 用于声明式配置 Provider 和 Module 的注册
    /// </summary>
    [CreateAssetMenu(fileName = "FrameworkConfig", menuName = "JulyGF/Framework Config", order = 0)]
    public class FrameworkConfig : ScriptableObject
    {
        [Header("资源系统配置")]
        [Tooltip("资源系统运行模式")]
        public JPlayMode PlayMode = JPlayMode.EditorSimulateMode;

        // [Tooltip("配置的格式")]
        // public ConfigDataFormat ConfigDataFormat = ConfigDataFormat.Json;
        
        [Header("事件总线配置")]
        public EventBusConfig EventBusConfig = new();
        
        [Header("任务时间重置配置")]
        public TaskResetConfig TaskResetConfig =  new(); 
        
        [Header("日志通道配置")]
        [Tooltip("启用的日志通道（控制普通日志输出，不影响 Warning/Error）")]
        public LogChannel EnabledLogChannels = LogChannel.All;
        
        [Header("Tip配置")]
        public TipConfig TipConfig = new();
    }
    
    /// <summary>
    /// Tip 配置
    /// </summary>
    [Serializable]
    public class TipConfig
    {
        [Tooltip("Tip 预制体资源路径")]
        public string TipPrefabPath = "UITipItem";
        
        [Tooltip("Tip 对象池最大数量")]
        public int PoolMaxSize = 10;
        
        [Tooltip("Tip 默认显示时长（秒）")]
        public float DefaultDuration = 2f;
        
        [Tooltip("Tip 淡出时长（秒）")]
        public float FadeOutDuration = 0.3f;
        
        [Tooltip("Tip 之间的间距")]
        public float Spacing = 10f;
        
        [Tooltip("Tip 上移动画时长")]
        public float MoveUpDuration = 0.2f;
        
        [Tooltip("Tip 入场动画起始偏移（从下方多少像素滑入）")]
        public float EnterOffset = 50f;
        
        [Tooltip("Tip 入场动画时长（秒）")]
        public float EnterDuration = 0.2f;
    }
}

