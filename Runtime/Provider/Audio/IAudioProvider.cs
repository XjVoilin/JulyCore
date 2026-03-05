using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace JulyCore.Provider.Audio
{
    /// <summary>
    /// 3D坐标
    /// </summary>
    public struct AudioPosition3D : IEquatable<AudioPosition3D>
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        private const float Precision = 1000f;

        public static AudioPosition3D Zero = new(0, 0, 0);

        public AudioPosition3D(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        private static int Quantize(float v)
        {
            return Mathf.RoundToInt(v * Precision);
        }

        public static bool operator ==(AudioPosition3D a, AudioPosition3D b)
        {
            return Quantize(a.X) == Quantize(b.X) && Quantize(a.Y) == Quantize(b.Y) && Quantize(a.Z) == Quantize(b.Z);
        }

        public static bool operator !=(AudioPosition3D a, AudioPosition3D b)
        {
            return !(a == b);
        }

        public bool Equals(AudioPosition3D other)
        {
            return Quantize(X) == Quantize(other.X) && Quantize(Y) == Quantize(other.Y) &&
                   Quantize(Z) == Quantize(other.Z);
        }

        public override bool Equals(object obj)
        {
            return obj is AudioPosition3D other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Quantize(X), Quantize(Y), Quantize(Z));
        }
    }

    /// <summary>
    /// 音频播放选项
    /// </summary>
    internal class AudioPlayOptions
    {
        /// <summary>
        /// 音量（0-1）
        /// </summary>
        public float Volume { get; set; } = 1f;

        /// <summary>
        /// 是否循环播放
        /// </summary>
        public bool Loop { get; set; } = false;

        /// <summary>
        /// 淡入时长（秒），0表示不淡入
        /// </summary>
        public float FadeInDuration { get; set; } = 0f;

        /// <summary>
        /// 优先级（0-256），数值越大优先级越高
        /// </summary>
        public int Priority { get; set; } = 128;

        /// <summary>
        /// 音调（0-3），1为正常音调
        /// </summary>
        public float Pitch { get; set; } = 1f;

        /// <summary>
        /// 延迟播放时长（秒），0表示立即播放
        /// </summary>
        public float Delay { get; set; } = 0f;

        /// <summary>
        /// 3D空间音效位置（世界坐标），如果为Vector3.zero则播放2D音效
        /// </summary>
        public AudioPosition3D Position { get; set; } = AudioPosition3D.Zero;

        /// <summary>
        /// 最小距离（3D音效衰减开始的距离）
        /// </summary>
        public float MinDistance { get; set; } = 1f;

        /// <summary>
        /// 最大距离（3D音效完全听不到的距离）
        /// </summary>
        public float MaxDistance { get; set; } = 500f;
    }

    /// <summary>
    /// 音频提供者接口
    /// 纯技术执行层：负责资源加载、AudioSource操作、播放控制
    /// 不包含任何业务语义，不维护业务状态
    /// 所有操作都基于 AudioHandle，由 Module 管理 Handle 的生命周期和业务逻辑
    /// </summary>
    internal interface IAudioProvider : Core.IProvider
    {
        #region 音频播放（纯技术操作）

        /// <summary>
        /// 播放音频（通过资源文件名，负责资源加载）
        /// </summary>
        /// <param name="fileName">音频文件名（不含扩展名）</param>
        /// <param name="options">播放选项，null使用默认选项</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>音频播放句柄，用于后续控制该音频</returns>
        UniTask<AudioHandle> PlayAudioAsync(string fileName, AudioPlayOptions options = null,
            CancellationToken cancellationToken = default);

        #endregion

        #region 音频控制（基于Handle的纯技术操作）

        /// <summary>
        /// 停止音频
        /// </summary>
        /// <param name="handle">音频播放句柄</param>
        /// <param name="fadeOutDuration">淡出时长（秒），0表示立即停止</param>
        void StopAudio(AudioHandle handle, float fadeOutDuration = 0f);

        /// <summary>
        /// 暂停音频
        /// </summary>
        /// <param name="handle">音频播放句柄</param>
        void PauseAudio(AudioHandle handle);

        /// <summary>
        /// 恢复音频
        /// </summary>
        /// <param name="handle">音频播放句柄</param>
        void ResumeAudio(AudioHandle handle);

        /// <summary>
        /// 设置音频音量
        /// </summary>
        /// <param name="handle">音频播放句柄</param>
        /// <param name="volume">音量（0-1）</param>
        void SetVolume(AudioHandle handle, float volume);

        /// <summary>
        /// 设置音频静音状态
        /// </summary>
        /// <param name="handle">音频播放句柄</param>
        /// <param name="mute">是否静音</param>
        void SetMute(AudioHandle handle, bool mute);

        /// <summary>
        /// 检查音频是否正在播放
        /// </summary>
        /// <param name="handle">音频播放句柄</param>
        /// <returns>是否正在播放</returns>
        bool IsPlaying(AudioHandle handle);

        #endregion

        #region 更新方法（由Module调用）

        /// <summary>
        /// 更新（用于清理无效句柄和检查播放完成）
        /// 由AudioModule调用
        /// </summary>
        /// <param name="realElapseSeconds">真实流逝时间（秒）</param>
        void Update(float realElapseSeconds);

        #endregion
    }

    /// <summary>
    /// 音频播放句柄
    /// 用于控制单个音频的播放，由 Provider 创建，由 Module 管理生命周期
    /// </summary>
    public class AudioHandle
    {
        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsValid { get; internal set; }

        /// <summary>
        /// 音频片段
        /// </summary>
        public AudioClip AudioClip { get; internal set; }

        /// <summary>
        /// 音频资源标识符（文件名）
        /// </summary>
        public string AudioIdentifier { get; internal set; }

        /// <summary>
        /// 音频源
        /// </summary>
        internal AudioSource AudioSource { get; set; }

        /// <summary>
        /// 优先级
        /// </summary>
        internal int Priority { get; set; }

        /// <summary>
        /// 基础音量（不考虑任何业务音量体系）
        /// </summary>
        internal float BaseVolume { get; set; } = 1f;

        /// <summary>
        /// 预计结束时间（Time.realtimeSinceStartup）
        /// 用于快速判断音频是否播放完成，避免每帧访问 isPlaying
        /// </summary>
        internal float ExpectedEndTime { get; set; }

        /// <summary>
        /// 是否暂停（Provider内部使用）
        /// </summary>
        internal bool IsPaused { get; set; }

        internal AudioHandle()
        {
            IsValid = true;
        }
    }
}