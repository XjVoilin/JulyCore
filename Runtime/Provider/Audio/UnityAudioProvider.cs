using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using JulyCore.Core;
using JulyCore.Provider.Base;
using JulyCore.Provider.Pool;
using JulyCore.Provider.Resource;
using UnityEngine;

namespace JulyCore.Provider.Audio
{
    /// <summary>
    /// Unity音效提供者实现
    /// 纯技术执行层：负责资源加载、AudioSource操作、播放控制
    /// 不包含任何业务语义，不维护业务状态
    /// </summary>
    internal class UnityAudioProvider : ProviderBase, IAudioProvider
    {
        public override int Priority => Frameworkconst.PriorityAudioProvider;
        protected override LogChannel LogChannel => LogChannel.Audio;

        private IResourceProvider _resourceProvider;
        private IPoolProvider _poolProvider;
        private IObjectPool<AudioSource> _audioSourcePool;

        // 管理所有活跃的 AudioHandle
        private readonly HashSet<AudioHandle> _activeHandles = new HashSet<AudioHandle>();

        /// <summary>
        /// 无效的handle
        /// </summary>
        private readonly List<AudioHandle> _invalidHandleList = new();

        // 用于淡入淡出的 Tweener 映射
        private readonly Dictionary<AudioHandle, Tweener> _fadeTweeners = new Dictionary<AudioHandle, Tweener>();

        // AudioSource 池配置
        private const int AudioSourcePoolInitialSize = 5;
        private const int AudioSourcePoolMaxSize = 20;

        /// <summary>
        /// 构造函数（依赖通过 DI 容器注入）
        /// </summary>
        public UnityAudioProvider(IResourceProvider resourceProvider, IPoolProvider poolProvider)
        {
            _resourceProvider = resourceProvider ?? throw new ArgumentNullException(nameof(resourceProvider));
            _poolProvider = poolProvider ?? throw new ArgumentNullException(nameof(poolProvider));
        }

        protected override UniTask OnInitAsync()
        {
            // 创建 AudioSource 对象池
            _audioSourcePool = _poolProvider.CreatePool(CreateAudioSource, OnGetAudioSource, OnReturnAudioSource,
                OnDestroyAudioSource, AudioSourcePoolInitialSize, AudioSourcePoolMaxSize);

            Log($"[{Name}] Unity音效提供者初始化完成");
            return UniTask.CompletedTask;
        }

        protected override UniTask OnShutdownAsync()
        {
            // 停止所有音频并清理
            var handlesToStop = new List<AudioHandle>(_activeHandles);
            foreach (var handle in handlesToStop)
            {
                StopAudio(handle);
            }

            // 清理对象池
            if (_poolProvider != null && _audioSourcePool != null)
            {
                _poolProvider.DestroyPool<AudioSource>();
                _audioSourcePool = null;
            }

            _activeHandles.Clear();
            _fadeTweeners.Clear();
            _resourceProvider = null;
            _poolProvider = null;

            Log($"[{Name}] Unity音效提供者已关闭");
            return UniTask.CompletedTask;
        }

        #region IAudioProvider 实现

        public async UniTask<AudioHandle> PlayAudioAsync(string fileName, AudioPlayOptions options = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                LogWarning($"[{Name}] 音频文件名不能为空");
                return null;
            }

            // 通过资源提供者加载音频
            var audioClip = await _resourceProvider.LoadAsync<AudioClip>(fileName, cancellationToken);
            if (audioClip == null)
            {
                LogWarning($"[{Name}] 加载音频失败: {fileName}");
                return null;
            }

            // 播放音频
            return PlayAudio(audioClip, options);
        }

        private AudioHandle PlayAudio(AudioClip audioClip, AudioPlayOptions options = null)
        {
            if (audioClip == null)
            {
                LogWarning($"[{Name}] AudioClip不能为null");
                return null;
            }

            // 获取或创建 AudioSource
            var audioSource = _audioSourcePool.Get();

            // 应用选项
            options ??= new AudioPlayOptions();
            ApplyOptionsToAudioSource(audioSource, options);

            // 设置 AudioClip
            audioSource.clip = audioClip;

            // 创建 Handle
            var handle = new AudioHandle
            {
                AudioClip = audioClip,
                AudioIdentifier = audioClip.name,
                AudioSource = audioSource,
                Priority = options.Priority,
                BaseVolume = options.Volume,
                ExpectedEndTime = CalculateExpectedEndTime(audioClip, options),
                IsPaused = false
            };

            // 记录 Handle
            _activeHandles.Add(handle);

            if (options.Delay > 0f)
            {
                audioSource.PlayDelayed(options.Delay);
            }
            else
            {
                audioSource.Play();
            }

            if (options.FadeInDuration > 0f)
            {
                audioSource.volume = 0f;
                StartFadeIn(handle, options.FadeInDuration, options.Delay);
            }

            return handle;
        }

        public void StopAudio(AudioHandle handle, float fadeOutDuration = 0f)
        {
            if (handle == null || !handle.IsValid)
            {
                return;
            }

            if (!_activeHandles.Contains(handle))
            {
                return;
            }

            // 停止淡入淡出动画
            StopFadeTween(handle);

            if (fadeOutDuration > 0f)
            {
                // 淡出
                StartFadeOut(handle, fadeOutDuration, () => { ActuallyStopAudio(handle); });
            }
            else
            {
                // 立即停止
                ActuallyStopAudio(handle);
            }
        }

        public void PauseAudio(AudioHandle handle)
        {
            if (handle == null || !handle.IsValid)
            {
                return;
            }

            if (!_activeHandles.Contains(handle))
            {
                return;
            }

            if (handle.IsPaused)
            {
                return;
            }

            handle.AudioSource.Pause();
            handle.IsPaused = true;
        }

        public void ResumeAudio(AudioHandle handle)
        {
            if (handle == null || !handle.IsValid)
            {
                return;
            }

            if (!_activeHandles.Contains(handle))
            {
                return;
            }

            if (!handle.IsPaused)
            {
                return;
            }

            handle.AudioSource.UnPause();
            handle.IsPaused = false;
        }

        public void SetVolume(AudioHandle handle, float volume)
        {
            if (handle == null || !handle.IsValid)
            {
                return;
            }

            if (!_activeHandles.Contains(handle))
            {
                return;
            }

            volume = Mathf.Clamp01(volume);
            handle.BaseVolume = volume;
            handle.AudioSource.volume = volume;
        }

        public void SetMute(AudioHandle handle, bool mute)
        {
            if (handle == null || !handle.IsValid)
            {
                return;
            }

            if (!_activeHandles.Contains(handle))
            {
                return;
            }

            handle.AudioSource.mute = mute;
        }

        public bool IsPlaying(AudioHandle handle)
        {
            if (handle == null || !handle.IsValid)
            {
                return false;
            }

            if (!_activeHandles.Contains(handle))
            {
                return false;
            }

            if (handle.IsPaused)
            {
                return false;
            }

            return handle.AudioSource.isPlaying;
        }

        public void Update(float realElapseSeconds)
        {
            foreach (var handle in _activeHandles)
            {
                // 检查 Handle 是否有效
                if (handle == null || !handle.IsValid)
                {
                    _invalidHandleList.Add(handle);
                    continue;
                }

                // 检查 AudioSource 是否有效
                if (handle.AudioSource == null)
                {
                    _invalidHandleList.Add(handle);
                    continue;
                }

                if (UnityEngine.Time.realtimeSinceStartup >= handle.ExpectedEndTime)
                {
                    if (!handle.IsPaused && !handle.AudioSource.loop && !handle.AudioSource.isPlaying)
                    {
                        _invalidHandleList.Add(handle);
                    }
                }
            }

            if (_invalidHandleList.Count > 0)
            {
                foreach (var handle in _invalidHandleList)
                {
                    CleanupHandle(handle);
                }

                _invalidHandleList.Clear();
            }
        }

        #endregion

        #region 私有辅助方法

        /// <summary>
        /// 创建 AudioSource
        /// </summary>
        private AudioSource CreateAudioSource()
        {
            // 创建一个 GameObject 用于挂载 AudioSource
            var go = new GameObject("AudioSource");
            go.transform.SetParent(null);
            UnityEngine.Object.DontDestroyOnLoad(go);

            var audioSource = go.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 默认2D

            return audioSource;
        }

        /// <summary>
        /// 从池中获取 AudioSource 时的回调
        /// </summary>
        private void OnGetAudioSource(AudioSource audioSource)
        {
            if (audioSource != null)
            {
                audioSource.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 将 AudioSource 返回池时的回调
        /// </summary>
        private void OnReturnAudioSource(AudioSource audioSource)
        {
            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.clip = null;
                audioSource.volume = 1f;
                audioSource.pitch = 1f;
                audioSource.mute = false;
                audioSource.loop = false;
                audioSource.spatialBlend = 0f;
                audioSource.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 销毁 AudioSource 时的回调
        /// </summary>
        private void OnDestroyAudioSource(AudioSource audioSource)
        {
            if (audioSource != null && audioSource.gameObject != null)
            {
                UnityEngine.Object.Destroy(audioSource.gameObject);
            }
        }

        /// <summary>
        /// 回收 AudioSource
        /// </summary>
        private void ReturnAudioSource(AudioSource audioSource)
        {
            if (_audioSourcePool != null && audioSource != null)
            {
                _audioSourcePool.Return(audioSource);
            }
            else if (audioSource != null && audioSource.gameObject != null)
            {
                // 如果没有对象池，直接销毁
                UnityEngine.Object.Destroy(audioSource.gameObject);
            }
        }

        /// <summary>
        /// 应用选项到 AudioSource
        /// </summary>
        private void ApplyOptionsToAudioSource(AudioSource audioSource, AudioPlayOptions options)
        {
            audioSource.volume = options.Volume;
            audioSource.loop = options.Loop;
            audioSource.priority = options.Priority;
            audioSource.pitch = options.Pitch;

            // 处理3D音效
            if (options.Position != AudioPosition3D.Zero)
            {
                audioSource.spatialBlend = 1f; // 3D
                audioSource.transform.position =
                    new Vector3(options.Position.X, options.Position.Y, options.Position.Z);
                audioSource.minDistance = options.MinDistance;
                audioSource.maxDistance = options.MaxDistance;
            }
            else
            {
                audioSource.spatialBlend = 0f; // 2D
            }
        }

        /// <summary>
        /// 计算预计结束时间
        /// </summary>
        private float CalculateExpectedEndTime(AudioClip clip, AudioPlayOptions options)
        {
            if (clip == null)
            {
                return 0f;
            }

            var clipDuration = clip.length / options.Pitch; // 考虑音调影响
            var delay = options.Delay;
            return UnityEngine.Time.realtimeSinceStartup + delay + clipDuration;
        }

        /// <summary>
        /// 延迟后开始淡入（用于延迟播放的场景）
        /// </summary>
        private void StartFadeIn(AudioHandle handle, float fadeInDuration, float delay = 0)
        {
            if (!_activeHandles.Contains(handle))
            {
                return;
            }

            // 停止之前的淡入淡出
            StopFadeTween(handle);

            // 确保初始音量为0
            var targetVolume = handle.BaseVolume;
            handle.AudioSource.volume = 0f;

            var tweener = handle.AudioSource.DOFade(targetVolume, fadeInDuration);
            if (delay > 0)
            {
                tweener = tweener.SetDelay(delay);
            }

            tweener.SetEase(Ease.Linear)
                .OnComplete(() => { _fadeTweeners.Remove(handle); });

            _fadeTweeners[handle] = tweener;
        }

        /// <summary>
        /// 开始淡出
        /// </summary>
        private void StartFadeOut(AudioHandle handle, float duration, Action onComplete)
        {
            if (!_activeHandles.Contains(handle))
            {
                onComplete?.Invoke();
                return;
            }

            // 使用 DOTween 淡出
            var tweener = handle.AudioSource.DOFade(0f, duration)
                .SetEase(Ease.Linear)
                .OnComplete(() =>
                {
                    _fadeTweeners.Remove(handle);
                    onComplete?.Invoke();
                });

            _fadeTweeners[handle] = tweener;
        }

        /// <summary>
        /// 停止淡入淡出动画
        /// </summary>
        private void StopFadeTween(AudioHandle handle)
        {
            if (_fadeTweeners.TryGetValue(handle, out var tweener))
            {
                tweener?.Kill();
                _fadeTweeners.Remove(handle);
            }
        }

        /// <summary>
        /// 实际停止音频
        /// </summary>
        private void ActuallyStopAudio(AudioHandle handle)
        {
            if (!_activeHandles.Contains(handle))
            {
                return;
            }

            // 停止 AudioSource
            if (handle.AudioSource != null)
            {
                handle.AudioSource.Stop();
            }

            // 清理 Handle
            CleanupHandle(handle);
        }

        /// <summary>
        /// 清理 Handle
        /// </summary>
        private void CleanupHandle(AudioHandle handle)
        {
            if (handle == null)
            {
                return;
            }

            // 标记 Handle 为无效
            handle.IsValid = false;

            // 停止淡入淡出动画
            StopFadeTween(handle);

            // 回收 AudioSource
            if (_activeHandles.Contains(handle))
            {
                if (handle.AudioSource != null)
                {
                    ReturnAudioSource(handle.AudioSource);
                }

                _activeHandles.Remove(handle);
            }
        }

        #endregion
    }
}