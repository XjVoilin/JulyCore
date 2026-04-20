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
            private static string _defaultClickSfx;
            private static bool _defaultClickSfxResolved;

            private static AudioModule Module
            {
                get
                {
                    _module ??= GetModule<AudioModule>();
                    return _module;
                }
            }

            public static string DefaultClickSfx
            {
                get
                {
                    if (!_defaultClickSfxResolved)
                    {
                        _defaultClickSfxResolved = true;
                        _defaultClickSfx = _context?.FrameworkConfig?.AudioConfig?.DefaultClickSfx;
                    }
                    return _defaultClickSfx;
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
                Module.PlayBGMAsync(fileName, options).Forget();
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
            public static bool IsBGMPlaying()
            {
                return Module.IsBGMPlaying();
            }

            #endregion

            #region SFX 操作

            /// <summary>
            /// 播放音效（2D）
            /// </summary>
            /// <param name="fileName">音频文件名（不含扩展名）</param>
            /// <param name="options">播放选项，null使用默认选项</param>
            public static void PlaySfx(string fileName, SfxPlayOptions options = null)
            {
                Module.PlaySfxAsync(fileName, options).Forget();
            }

            /// <summary>
            /// 播放音效（3D空间音效）
            /// </summary>
            /// <param name="fileName">音频文件名（不含扩展名）</param>
            /// <param name="options">播放选项，必须指定Position</param>
            public static void PlaySfx3D(string fileName, Sfx3DPlayOptions options)
            {
                Module.PlaySfx3DAsync(fileName, options).Forget();
            }

            /// <summary>
            /// 停止音效（通过句柄）
            /// </summary>
            public static void StopSfx(AudioHandle handle)
            {
                Module.StopSfx(handle);
            }

            /// <summary>
            /// 停止音效（通过文件名）
            /// </summary>
            public static void StopSfx(string fileName)
            {
                Module.StopSfx(fileName);
            }

            /// <summary>
            /// 停止所有音效
            /// </summary>
            public static void StopAllSfx()
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
            public static void SetSfxVolume(float volume)
            {
                Module.SetSfxVolume(volume);
            }

            /// <summary>
            /// 获取音效音量
            /// </summary>
            /// <returns>音量（0-1）</returns>
            public static float GetSfxVolume()
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
            public static void SetSfxMute(bool mute)
            {
                Module.SetSfxMute(mute);
            }

            /// <summary>
            /// 获取音效静音状态
            /// </summary>
            /// <returns>是否静音</returns>
            public static bool IsSfxMuted()
            {
                return Module.IsSfxMuted();
            }

            #endregion
        }
    }
}