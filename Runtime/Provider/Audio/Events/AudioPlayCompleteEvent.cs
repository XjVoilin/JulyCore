using JulyCore.Core;

namespace JulyCore.Provider.Audio.Events
{
    /// <summary>
    /// 音频播放完成事件
    /// 当音频播放完成或被停止时触发
    /// </summary>
    public class AudioPlayCompleteEvent : IEvent
    {
        /// <summary>
        /// 音频名称（AudioClip的name）
        /// </summary>
        public string AudioName { get; set; }

        /// <summary>
        /// 是否是被停止（true表示被停止，false表示自然播放完成）
        /// </summary>
        public bool WasStopped { get; set; }

        public AudioPlayCompleteEvent(string audioName, bool wasStopped)
        {
            AudioName = audioName;
            WasStopped = wasStopped;
        }
    }
}

