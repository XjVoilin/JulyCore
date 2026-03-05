using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Module.Base;
using JulyCore.Provider.Audio;

namespace JulyCore.Module.Audio
{
    /// <summary>
    /// BGM播放选项（业务语义）
    /// </summary>
    public class BGMPlayOptions
    {
        /// <summary>
        /// 是否循环播放
        /// </summary>
        public bool Loop { get; set; } = true;

        /// <summary>
        /// 音量（0-1），null表示使用当前BGM音量
        /// </summary>
        public float? Volume { get; set; } = null;

        /// <summary>
        /// 淡入时长（秒），0表示不淡入
        /// </summary>
        public float FadeInDuration { get; set; } = 0f;

        /// <summary>
        /// 淡出时长（秒），切换BGM时使用，0表示立即停止
        /// </summary>
        public float FadeOutDuration { get; set; } = 0f;
    }

    /// <summary>
    /// SFX播放选项（2D，业务语义）
    /// </summary>
    public class SfxPlayOptions
    {
        /// <summary>
        /// 音量（0-1），null表示使用当前SFX音量
        /// </summary>
        public float? Volume { get; set; } = null;

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
        /// 音频分组，用于分组管理
        /// </summary>
        public string Group { get; set; } = null;
    }

    /// <summary>
    /// SFX播放选项（3D空间音效，业务语义）
    /// </summary>
    public class Sfx3DPlayOptions : SfxPlayOptions
    {
        /// <summary>
        /// 播放位置（世界坐标）
        /// </summary>
        public AudioPosition3D Position { get; set; }

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
    /// 音频模块
    /// 业务语义与流程调度层：决定BGM/SFX分类、是否打断/切换、播放顺序
    /// 管理 AudioHandle 的生命周期
    /// 不直接操作 Unity 对象，不负责资源加载
    /// </summary>
    internal class AudioModule : ModuleBase
    {
        private IAudioProvider _audioProvider;

        protected override LogChannel LogChannel => LogChannel.Audio;

        // BGM相关业务状态
        private AudioHandle _currentBgmHandle;
        private float _bgmVolume = 1f;
        private bool _bgmMute = false;

        // SFX相关业务状态
        private readonly Dictionary<AudioHandle, SfxInfo> _activeSfxHandles = new Dictionary<AudioHandle, SfxInfo>();
        private float _sfxVolume = 1f;
        private bool _sfxMute = false;

        // 主音量控制
        private float _masterVolume = 1f;
        private bool _masterMute = false;

        private readonly List<AudioHandle> _invalidHandleList = new();

        /// <summary>
        /// 模块执行优先级
        /// </summary>
        public override int Priority => Frameworkconst.PriorityAudioModule;

        /// <summary>
        /// SFX信息
        /// </summary>
        private class SfxInfo
        {
            public string Group { get; set; }
            public float BaseVolume { get; set; }
        }

        /// <summary>
        /// 初始化Module
        /// </summary>
        protected override async UniTask OnInitAsync()
        {
            try
            {
                _audioProvider = GetProvider<IAudioProvider>();
                Log($"[{Name}] 音效模块初始化完成");
                await base.OnInitAsync();
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 初始化失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 更新（用于清理无效句柄）
        /// </summary>
        protected override void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            _audioProvider.Update(realElapseSeconds);

            // 清理无效的BGM句柄
            if (_currentBgmHandle != null && !_currentBgmHandle.IsValid)
            {
                _currentBgmHandle = null;
            }

            foreach (var kvp in _activeSfxHandles)
            {
                if (kvp.Key == null || !kvp.Key.IsValid)
                {
                    _invalidHandleList.Add(kvp.Key);
                }
            }

            if (_invalidHandleList.Count > 0)
            {
                foreach (var handle in _invalidHandleList)
                {
                    _activeSfxHandles.Remove(handle);
                }

                _invalidHandleList.Clear();
            }
        }

        #region 背景音乐（BGM）

        /// <summary>
        /// 播放背景音乐（通过资源文件名）
        /// </summary>
        internal async UniTask<bool> PlayBGMAsync(string fileName, BGMPlayOptions options = null,
            CancellationToken cancellationToken = default)
        {
            // 停止当前BGM（如果有）
            if (_currentBgmHandle != null)
            {
                var fadeOutDuration = options?.FadeOutDuration ?? 0f;
                _audioProvider.StopAudio(_currentBgmHandle, fadeOutDuration);
                _currentBgmHandle = null;
            }

            // 构建参数
            var techOptions = new AudioPlayOptions
            {
                Loop = options?.Loop ?? true,
                Volume = CalculateBGMVolume(options?.Volume),
                FadeInDuration = options?.FadeInDuration ?? 0f
            };

            // 通过Provider加载并播放
            var handle = await _audioProvider.PlayAudioAsync(fileName, techOptions, cancellationToken);
            if (handle == null)
            {
                LogWarning($"[{Name}] 播放BGM失败: {fileName}");
                return false;
            }

            _currentBgmHandle = handle;
            return true;
        }

        /// <summary>
        /// 停止背景音乐
        /// </summary>
        internal void StopBGM(float fadeOutDuration = 0f)
        {
            if (_currentBgmHandle == null)
            {
                return;
            }

            _audioProvider.StopAudio(_currentBgmHandle, fadeOutDuration);
            _currentBgmHandle = null;
        }

        /// <summary>
        /// 暂停背景音乐
        /// </summary>
        internal void PauseBGM()
        {
            if (_currentBgmHandle == null)
            {
                return;
            }

            _audioProvider.PauseAudio(_currentBgmHandle);
        }

        /// <summary>
        /// 恢复背景音乐
        /// </summary>
        internal void ResumeBGM()
        {
            if (_currentBgmHandle == null)
            {
                return;
            }

            _audioProvider.ResumeAudio(_currentBgmHandle);
        }

        /// <summary>
        /// 检查背景音乐是否正在播放
        /// </summary>
        internal bool IsBGMPlaying()
        {
            if (_currentBgmHandle == null)
            {
                return false;
            }

            return _audioProvider.IsPlaying(_currentBgmHandle);
        }

        /// <summary>
        /// 获取当前播放的背景音乐句柄
        /// </summary>
        internal AudioHandle GetCurrentBGMHandle()
        {
            return _currentBgmHandle;
        }

        #endregion

        #region 音效（SFX）

        /// <summary>
        /// 播放音效（2D，通过资源文件名）
        /// </summary>
        internal async UniTask<AudioHandle> PlaySfxAsync(string fileName, SfxPlayOptions options = null,
            CancellationToken cancellationToken = default)
        {
            // 构建技术参数
            var techOptions = new AudioPlayOptions
            {
                Volume = CalculateSfxVolume(options?.Volume),
                Priority = options?.Priority ?? 128,
                Pitch = options?.Pitch ?? 1f,
                FadeInDuration = 0f,
                Delay = options?.Delay ?? 0f,
                Loop = false
            };

            // 通过Provider加载并播放
            var handle = await _audioProvider.PlayAudioAsync(fileName, techOptions, cancellationToken);
            if (handle == null)
            {
                LogWarning($"[{Name}] 播放SFX失败: {fileName}");
                return null;
            }

            // 记录业务信息
            _activeSfxHandles[handle] = new SfxInfo
            {
                Group = options?.Group,
                BaseVolume = techOptions.Volume
            };

            return handle;
        }

        /// <summary>
        /// 播放音效（3D空间音效，通过资源文件名）
        /// </summary>
        internal async UniTask<AudioHandle> PlaySfx3DAsync(string fileName, Sfx3DPlayOptions options,
            CancellationToken cancellationToken = default)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options), "Sfx3DPlayOptions不能为null，必须指定Position");
            }

            // 构建技术参数
            var techOptions = new AudioPlayOptions
            {
                Volume = CalculateSfxVolume(options.Volume),
                Priority = options.Priority,
                Pitch = options.Pitch,
                FadeInDuration = 0,
                Delay = options.Delay,
                Loop = false,
                Position = new AudioPosition3D(options.Position.X, options.Position.Y, options.Position.Z),
                MinDistance = options.MinDistance,
                MaxDistance = options.MaxDistance
            };

            // 通过Provider加载并播放
            var handle = await _audioProvider.PlayAudioAsync(fileName, techOptions, cancellationToken);
            if (handle == null)
            {
                LogWarning($"[{Name}] 播放SFX3D失败: {fileName}");
                return null;
            }

            // 记录业务信息
            _activeSfxHandles[handle] = new SfxInfo
            {
                Group = options.Group,
                BaseVolume = techOptions.Volume
            };

            return handle;
        }

        /// <summary>
        /// 停止音效
        /// </summary>
        internal void StopSfx(AudioHandle handle)
        {
            if (handle == null)
            {
                return;
            }

            _audioProvider.StopAudio(handle);
            _activeSfxHandles.Remove(handle);
        }

        /// <summary>
        /// 停止所有音效
        /// </summary>
        internal void StopAllSfx()
        {
            var handles = new List<AudioHandle>(_activeSfxHandles.Keys);
            foreach (var handle in handles)
            {
                if (handle != null && handle.IsValid)
                {
                    _audioProvider.StopAudio(handle);
                }
            }

            _activeSfxHandles.Clear();
        }

        /// <summary>
        /// 停止指定分组的所有音效
        /// </summary>
        internal void StopSfxByGroup(string group)
        {
            if (string.IsNullOrEmpty(group))
            {
                return;
            }

            var handlesToStop = new List<AudioHandle>();
            foreach (var kvp in _activeSfxHandles)
            {
                if (kvp.Value.Group == group && kvp.Key != null && kvp.Key.IsValid)
                {
                    handlesToStop.Add(kvp.Key);
                }
            }

            foreach (var handle in handlesToStop)
            {
                _audioProvider.StopAudio(handle);
                _activeSfxHandles.Remove(handle);
            }
        }

        #endregion

        #region 音量控制（业务层）

        /// <summary>
        /// 设置主音量
        /// </summary>
        internal void SetMasterVolume(float volume)
        {
            _masterVolume = Math.Clamp(volume, 0f, 1f);
            ApplyVolumeToAll();
        }

        /// <summary>
        /// 获取主音量
        /// </summary>
        internal float GetMasterVolume()
        {
            return _masterVolume;
        }

        /// <summary>
        /// 设置背景音乐音量
        /// </summary>
        internal void SetBGMVolume(float volume)
        {
            _bgmVolume = Math.Clamp(volume, 0f, 1f);
            ApplyVolumeToBGM();
        }

        /// <summary>
        /// 获取背景音乐音量
        /// </summary>
        internal float GetBGMVolume()
        {
            return _bgmVolume;
        }

        /// <summary>
        /// 设置音效音量
        /// </summary>
        internal void SetSfxVolume(float volume)
        {
            _sfxVolume = Math.Clamp(volume, 0f, 1f);
            ApplyVolumeToSFX();
        }

        /// <summary>
        /// 获取音效音量
        /// </summary>
        internal float GetSfxVolume()
        {
            return _sfxVolume;
        }

        /// <summary>
        /// 设置主音量静音状态
        /// </summary>
        internal void SetMasterMute(bool mute)
        {
            _masterMute = mute;
            ApplyMuteToAll();
        }

        /// <summary>
        /// 获取主音量静音状态
        /// </summary>
        internal bool IsMasterMuted()
        {
            return _masterMute;
        }

        /// <summary>
        /// 设置背景音乐静音状态
        /// </summary>
        internal void SetBGMMute(bool mute)
        {
            _bgmMute = mute;
            ApplyMuteToBGM();
        }

        /// <summary>
        /// 获取背景音乐静音状态
        /// </summary>
        internal bool IsBGMMuted()
        {
            return _bgmMute;
        }

        /// <summary>
        /// 设置音效静音状态
        /// </summary>
        internal void SetSfxMute(bool mute)
        {
            _sfxMute = mute;
            ApplyMuteToSFX();
        }

        /// <summary>
        /// 获取音效静音状态
        /// </summary>
        internal bool IsSfxMuted()
        {
            return _sfxMute;
        }

        #endregion

        #region 私有辅助方法

        /// <summary>
        /// 计算BGM音量（业务层逻辑）
        /// </summary>
        private float CalculateBGMVolume(float? volume)
        {
            var baseVolume = volume ?? _bgmVolume;
            return baseVolume * _masterVolume;
        }

        /// <summary>
        /// 计算SFX音量（业务层逻辑）
        /// </summary>
        private float CalculateSfxVolume(float? volume)
        {
            var baseVolume = volume ?? _sfxVolume;
            return baseVolume * _masterVolume;
        }

        /// <summary>
        /// 应用音量到所有音频
        /// </summary>
        private void ApplyVolumeToAll()
        {
            ApplyVolumeToBGM();
            ApplyVolumeToSFX();
        }

        /// <summary>
        /// 应用音量到BGM
        /// </summary>
        private void ApplyVolumeToBGM()
        {
            if (_currentBgmHandle == null)
            {
                return;
            }

            var finalVolume = _bgmVolume * _masterVolume;
            _audioProvider.SetVolume(_currentBgmHandle, finalVolume);
        }

        /// <summary>
        /// 应用音量到SFX
        /// </summary>
        private void ApplyVolumeToSFX()
        {
            var finalVolume = _sfxVolume * _masterVolume;
            foreach (var kvp in _activeSfxHandles)
            {
                if (kvp.Key != null && kvp.Key.IsValid)
                {
                    var handleVolume = kvp.Value.BaseVolume * finalVolume;
                    _audioProvider.SetVolume(kvp.Key, handleVolume);
                }
            }
        }

        /// <summary>
        /// 应用静音到所有音频
        /// </summary>
        private void ApplyMuteToAll()
        {
            ApplyMuteToBGM();
            ApplyMuteToSFX();
        }

        /// <summary>
        /// 应用静音到BGM
        /// </summary>
        private void ApplyMuteToBGM()
        {
            if (_currentBgmHandle == null)
            {
                return;
            }

            _audioProvider.SetMute(_currentBgmHandle, _masterMute || _bgmMute);
        }

        /// <summary>
        /// 应用静音到SFX
        /// </summary>
        private void ApplyMuteToSFX()
        {
            bool finalMute = _masterMute || _sfxMute;
            foreach (var kvp in _activeSfxHandles)
            {
                if (kvp.Key != null && kvp.Key.IsValid)
                {
                    _audioProvider.SetMute(kvp.Key, finalMute);
                }
            }
        }

        #endregion

        /// <summary>
        /// 关闭Module
        /// </summary>
        protected override async UniTask OnShutdownAsync()
        {
            // 停止所有音频
            StopBGM(0f);
            StopAllSfx();

            _audioProvider = null;
            _currentBgmHandle = null;
            _activeSfxHandles.Clear();
            _invalidHandleList.Clear();

            Log($"[{Name}] 音效模块已关闭");
            await base.OnShutdownAsync();
        }
    }
}