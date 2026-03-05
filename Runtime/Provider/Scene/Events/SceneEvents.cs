using JulyCore.Core;
using UnityEngine.SceneManagement;

namespace JulyCore.Provider.Scene.Events
{
    /// <summary>
    /// 场景加载开始事件
    /// </summary>
    public class SceneLoadStartEvent : IEvent
    {
        public string SceneName { get; set; }
        public LoadSceneMode LoadMode { get; set; }
    }

    /// <summary>
    /// 场景加载完成事件
    /// </summary>
    public class SceneLoadCompleteEvent : IEvent
    {
        public string SceneName { get; set; }
        public UnityEngine.SceneManagement.Scene Scene { get; set; }
        public LoadSceneMode LoadMode { get; set; }
    }

    /// <summary>
    /// 场景卸载开始事件
    /// </summary>
    public class SceneUnloadStartEvent : IEvent
    {
        public string SceneName { get; set; }
    }

    /// <summary>
    /// 场景卸载完成事件
    /// </summary>
    public class SceneUnloadCompleteEvent : IEvent
    {
        public string SceneName { get; set; }
        public bool Success { get; set; }
    }

    /// <summary>
    /// 场景切换开始事件
    /// </summary>
    public class SceneSwitchStartEvent : IEvent
    {
        public string FromSceneName { get; set; }
        public string ToSceneName { get; set; }
    }

    /// <summary>
    /// 场景切换完成事件
    /// </summary>
    public class SceneSwitchCompleteEvent : IEvent
    {
        public string FromSceneName { get; set; }
        public string ToSceneName { get; set; }
        public UnityEngine.SceneManagement.Scene Scene { get; set; }
    }
}

