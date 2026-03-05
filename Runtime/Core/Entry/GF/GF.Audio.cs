using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Module.Audio;
using JulyCore.Provider.Audio;

namespace JulyCore
{
    public static partial class GF
    {
        /// <summary>
        /// 音频相关操作
        /// </summary>
        public static class Audio
        {
            private static AudioModule _module;

            private static AudioModule Module
            {
                get
                {
                    _module ??= GetModule<AudioModule>();
                    return _module;
                }
            }

            #region BGM 操作

            /// <summary>
            /// 播放背景音乐
            /// </summary>
            /// <param name="fileName">音频文件名（不含扩展名）</param>
            /// <param name="options">播放选项，null使用默认选项</param>
            public static void PlayBGM(string fileName, BGMPlayOptions options = null)
            {
                Module.PlayBGMAsync(fileName, options, _context.CancellationToken).Forget();
            }

            /// <summary>
            /// 播放背景音乐
            /// </summary>
            /// <param name="fileName">音频文件名（不含扩展名）</param>
            /// <param name="options">播放选项，null使用默认选项</param>
            /// <returns>是否播放成功</returns>
            public static async UniTask<bool> PlayBGMAsync(string fileName, BGMPlayOptions options = null)
            {
                return await Module.PlayBGMAsync(fileName, options, _context.CancellationToken);
            }

            /// <summary>
            /// 停止背景音乐
            /// </summary>
            /// <param name="fadeOutDuration">淡出时长（秒），0表示立即停止</param>
            public static void StopBGM(float fadeOutDuration = 0f)
            {
                Module.StopBGM(fadeOutDuration);
            }

            /// <summary>
            /// 暂停背景音乐
            /// </summary>
            public static void PauseBGM()
            {
                Module.PauseBGM();
            }

            /// <summary>
            /// 恢复背景音乐
            /// </summary>
            public static void ResumeBGM()
            {
                Module.ResumeBGM();
            }

            /// <summary>
            /// 检查背景音乐是否正在播放
            /// </summary>
            /// <returns>是否正在播放</returns>
            public static bool IsBGMPlaying()
            {
                return Module.IsBGMPlaying();
            }

            #endregion

            #region SFX 操作

            /// <summary>
            /// 播放音效（2D，通过资源文件名，使用配置类）
            /// </summary>
            /// <param name="fileName">音频文件名（不含扩展名）</param>
            /// <param name="options">播放选项，null使用默认选项</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>音效播放句柄，可用于停止音效</returns>
            public static async UniTask<AudioHandle> PlaySFXAsync(string fileName, SfxPlayOptions options = null,
                CancellationToken cancellationToken = default)
            {
                return await Module.PlaySfxAsync(fileName, options, cancellationToken);
            }

            /// <summary>
            /// 播放音效（3D空间音效，通过资源文件名，使用配置类）
            /// </summary>
            /// <param name="fileName">音频文件名（不含扩展名）</param>
            /// <param name="options">播放选项，必须指定Position</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>音效播放句柄，可用于停止音效</returns>
            public static async UniTask<AudioHandle> PlaySFX3DAsync(string fileName, Sfx3DPlayOptions options,
                CancellationToken cancellationToken = default)
            {
                return await Module.PlaySfx3DAsync(fileName, options, cancellationToken);
            }

            /// <summary>
            /// 停止音效
            /// </summary>
            /// <param name="handle">音效播放句柄</param>
            /// <param name="fadeOutDuration">淡出时长（秒），0表示立即停止</param>
            public static void StopSFX(AudioHandle handle)
            {
                Module.StopSfx(handle);
            }

            /// <summary>
            /// 停止所有音效
            /// </summary>
            public static void StopAllSFX()
            {
                Module.StopAllSfx();
            }

            #endregion

            #region 音量控制

            /// <summary>
            /// 设置主音量
            /// </summary>
            /// <param name="volume">音量（0-1）</param>
            public static void SetMasterVolume(float volume)
            {
                Module.SetMasterVolume(volume);
            }

            /// <summary>
            /// 获取主音量
            /// </summary>
            /// <returns>音量（0-1）</returns>
            public static float GetMasterVolume()
            {
                return Module.GetMasterVolume();
            }

            /// <summary>
            /// 设置背景音乐音量
            /// </summary>
            /// <param name="volume">音量（0-1）</param>
            public static void SetBGMVolume(float volume)
            {
                Module.SetBGMVolume(volume);
            }

            /// <summary>
            /// 获取背景音乐音量
            /// </summary>
            /// <returns>音量（0-1）</returns>
            public static float GetBGMVolume()
            {
                return Module.GetBGMVolume();
            }

            /// <summary>
            /// 设置音效音量
            /// </summary>
            /// <param name="volume">音量（0-1）</param>
            public static void SetSFXVolume(float volume)
            {
                Module.SetSfxVolume(volume);
            }

            /// <summary>
            /// 获取音效音量
            /// </summary>
            /// <returns>音量（0-1）</returns>
            public static float GetSFXVolume()
            {
                return Module.GetSfxVolume();
            }

            #endregion

            #region 静音控制

            /// <summary>
            /// 设置主音量静音状态
            /// </summary>
            /// <param name="mute">是否静音</param>
            public static void SetMasterMute(bool mute)
            {
                Module.SetMasterMute(mute);
            }

            /// <summary>
            /// 获取主音量静音状态
            /// </summary>
            /// <returns>是否静音</returns>
            public static bool IsMasterMuted()
            {
                return Module.IsMasterMuted();
            }

            /// <summary>
            /// 设置背景音乐静音状态
            /// </summary>
            /// <param name="mute">是否静音</param>
            public static void SetBGMMute(bool mute)
            {
                Module.SetBGMMute(mute);
            }

            /// <summary>
            /// 获取背景音乐静音状态
            /// </summary>
            /// <returns>是否静音</returns>
            public static bool IsBGMMuted()
            {
                return Module.IsBGMMuted();
            }

            /// <summary>
            /// 设置音效静音状态
            /// </summary>
            /// <param name="mute">是否静音</param>
            public static void SetSFXMute(bool mute)
            {
                Module.SetSfxMute(mute);
            }

            /// <summary>
            /// 获取音效静音状态
            /// </summary>
            /// <returns>是否静音</returns>
            public static bool IsSFXMuted()
            {
                return Module.IsSfxMuted();
            }

            #endregion
        }
    }
}