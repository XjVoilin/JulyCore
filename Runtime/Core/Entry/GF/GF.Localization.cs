using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Module.Localization;

namespace JulyCore
{
    public static partial class GF
    {
        /// <summary>
        /// 多语言相关操作
        /// </summary>
        public static class Localization
        {
            private static LocalizationModule _module;
            private static LocalizationModule Module
            {
                get
                {
                    _module ??= GetModule<LocalizationModule>();
                    return _module;
                }
            }
            
            #region 属性

            /// <summary>
            /// 获取当前语言代码
            /// </summary>
            public static string CurrentLanguage => Module.CurrentLanguage;

            /// <summary>
            /// 获取默认语言代码
            /// </summary>
            public static string DefaultLanguage => Module.DefaultLanguage;

            /// <summary>
            /// 获取支持的语言列表
            /// </summary>
            public static IReadOnlyList<string> SupportedLanguages => Module.SupportedLanguages ?? Array.Empty<string>();

            #endregion

            #region 语言管理

            /// <summary>
            /// 设置当前语言
            /// </summary>
            /// <param name="languageCode">语言代码（如 "zh-CN", "en-US"）</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>是否设置成功</returns>
            public static async UniTask<bool> SetLanguageAsync(string languageCode,
                CancellationToken cancellationToken = default)
            {
                return await Module.SetLanguageAsync(languageCode, cancellationToken);
            }

            /// <summary>
            /// 加载语言包
            /// </summary>
            /// <param name="languageCode">语言代码</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>是否加载成功</returns>
            public static async UniTask<bool> LoadLanguageAsync(string languageCode,
                CancellationToken cancellationToken = default)
            {
                return await Module.LoadLanguageAsync(languageCode, cancellationToken);
            }

            /// <summary>
            /// 卸载语言包
            /// </summary>
            /// <param name="languageCode">语言代码</param>
            public static void UnloadLanguage(string languageCode)
            {
                Module.UnloadLanguage(languageCode);
            }

            #endregion

            #region 文本本地化

            /// <summary>
            /// 获取本地化文本
            /// </summary>
            /// <param name="key">文本键</param>
            /// <param name="defaultValue">默认值（如果key不存在）</param>
            /// <returns>本地化文本</returns>
            public static string Get(string key, string defaultValue = null)
            {
                return Module.Get(key, defaultValue);
            }

            /// <summary>
            /// 获取本地化文本（简写，等同于 Get）
            /// </summary>
            /// <param name="key">文本键</param>
            /// <returns>本地化文本</returns>
            public static string T(string key)
            {
                return Get(key);
            }

            /// <summary>
            /// 获取本地化文本（带参数格式化）
            /// </summary>
            /// <param name="key">文本键</param>
            /// <param name="args">格式化参数</param>
            /// <returns>格式化后的本地化文本</returns>
            public static string GetFormat(string key, params object[] args)
            {
                return Module.GetFormat(key, args);
            }

            /// <summary>
            /// 获取本地化文本（带参数格式化，简写）
            /// </summary>
            /// <param name="key">文本键</param>
            /// <param name="args">格式化参数</param>
            /// <returns>格式化后的本地化文本</returns>
            public static string TF(string key, params object[] args)
            {
                return GetFormat(key, args);
            }

            /// <summary>
            /// 检查键是否存在
            /// </summary>
            /// <param name="key">文本键</param>
            /// <returns>是否存在</returns>
            public static bool HasKey(string key)
            {
                return Module.HasKey(key);
            }

            /// <summary>
            /// 获取所有键
            /// </summary>
            /// <param name="languageCode">语言代码（null表示当前语言）</param>
            /// <returns>键列表</returns>
            public static IReadOnlyList<string> GetAllKeys(string languageCode = null)
            {
                return Module.GetAllKeys(languageCode);
            }

            #endregion
        }
    }
}