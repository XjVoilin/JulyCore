using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Module.Base;
using JulyCore.Provider.Localization;
using UnityEngine;

namespace JulyCore.Module.Localization
{
    /// <summary>
    /// 多语言模块
    /// 业务语义与流程调度层：决定语言切换规则、默认语言回退策略、系统语言检测
    /// 管理语言状态和业务逻辑
    /// 不直接操作资源加载，不负责数据存储格式
    /// </summary>
    internal class LocalizationModule : ModuleBase
    {
        private ILocalizationProvider _localizationProvider;

        protected override LogChannel LogChannel => LogChannel.Localization;

        // 业务状态
        private string _currentLanguage = "CN";
        private string _defaultLanguage = "CN";
        private readonly List<string> _supportedLanguages = new List<string>();

        public override int Priority => Frameworkconst.PriorityLocalizationModule;

        /// <summary>
        /// 当前语言代码（业务状态）
        /// </summary>
        internal string CurrentLanguage => _currentLanguage;

        /// <summary>
        /// 默认语言代码（业务状态）
        /// </summary>
        internal string DefaultLanguage => _defaultLanguage;

        /// <summary>
        /// 支持的语言列表（业务状态）
        /// </summary>
        internal IReadOnlyList<string> SupportedLanguages => _supportedLanguages;

        protected override async UniTask OnInitAsync()
        {
            try
            {
                _localizationProvider = GetProvider<ILocalizationProvider>();
                EnsureProvider();

                // 初始化支持的语言列表（业务规则）
                InitializeSupportedLanguages();

                // 检测系统语言（业务规则）
                var detectedLanguage = DetectSystemLanguage();
                if (_supportedLanguages.Contains(detectedLanguage))
                {
                    _currentLanguage = detectedLanguage;
                }

                // 加载当前语言包
                await LoadLanguageAsync(_currentLanguage);

                await base.OnInitAsync();
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 多语言模块初始化失败: {ex.Message}");
                throw;
            }
        }

        #region 语言管理（业务层）

        /// <summary>
        /// 设置当前语言（业务规则：验证、切换、事件发布）
        /// </summary>
        /// <param name="languageCode">语言代码</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否设置成功</returns>
        internal async UniTask<bool> SetLanguageAsync(string languageCode, CancellationToken cancellationToken = default)
        {
            EnsureProvider();

            // 业务验证
            if (string.IsNullOrEmpty(languageCode))
            {
                LogWarning($"[{Name}] 语言代码不能为空");
                return false;
            }

            if (languageCode == _currentLanguage)
            {
                return true;
            }

            if (!_supportedLanguages.Contains(languageCode))
            {
                LogWarning($"[{Name}] 不支持的语言: {languageCode}");
                return false;
            }

            // 确保语言包已加载
            if (!_localizationProvider.IsLanguageLoaded(languageCode))
            {
                var success = await LoadLanguageAsync(languageCode, cancellationToken);
                if (!success)
                {
                    return false;
                }
            }

            // 更新业务状态
            var oldLanguage = _currentLanguage;
            _currentLanguage = languageCode;

            // 发布业务事件
            try
            {
                var e = new LanguageChangedEvent(oldLanguage, _currentLanguage);
                EventBus.Publish(e);
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 语言变更事件处理异常: {ex.Message}");
            }

            return true;
        }

        /// <summary>
        /// 加载语言包（业务层调用，内部调用Provider）
        /// </summary>
        /// <param name="languageCode">语言代码</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否加载成功</returns>
        internal async UniTask<bool> LoadLanguageAsync(string languageCode, CancellationToken cancellationToken = default)
        {
            EnsureProvider();
            return await _localizationProvider.LoadLanguageAsync(languageCode, cancellationToken);
        }

        /// <summary>
        /// 卸载语言包（业务规则：不能卸载当前语言）
        /// </summary>
        /// <param name="languageCode">语言代码</param>
        internal void UnloadLanguage(string languageCode)
        {
            EnsureProvider();

            // 业务规则：不能卸载当前语言
            if (languageCode == _currentLanguage)
            {
                LogWarning($"[{Name}] 无法卸载当前语言: {languageCode}");
                return;
            }

            _localizationProvider.UnloadLanguage(languageCode);
        }

        #endregion

        #region 文本访问（业务层：包含默认语言回退策略）

        /// <summary>
        /// 获取本地化文本（业务规则：当前语言 → 默认语言 → 默认值）
        /// </summary>
        /// <param name="key">文本键</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>本地化文本</returns>
        internal string Get(string key, string defaultValue = null)
        {
            EnsureProvider();

            if (string.IsNullOrEmpty(key))
            {
                return defaultValue ?? string.Empty;
            }

            // 从当前语言获取
            var text = _localizationProvider.GetText(_currentLanguage, key, null);
            if (text != null && text != key)
            {
                return text;
            }

            // 从默认语言获取（业务规则：默认语言回退）
            if (_currentLanguage != _defaultLanguage)
            {
                text = _localizationProvider.GetText(_defaultLanguage, key, null);
                if (text != null && text != key)
                {
                    return text;
                }
            }

            // 返回默认值或key
            return defaultValue ?? key;
        }

        /// <summary>
        /// 获取本地化文本（带参数格式化）
        /// </summary>
        /// <param name="key">文本键</param>
        /// <param name="args">格式化参数</param>
        /// <returns>格式化后的本地化文本</returns>
        internal string GetFormat(string key, params object[] args)
        {
            var text = Get(key);
            if (args == null || args.Length == 0)
            {
                return text;
            }

            try
            {
                return string.Format(text, args);
            }
            catch (FormatException ex)
            {
                LogWarning($"[{Name}] 格式化失败 ({key}): {ex.Message}");
                return text;
            }
        }

        /// <summary>
        /// 检查键是否存在（业务规则：检查当前语言和默认语言）
        /// </summary>
        /// <param name="key">文本键</param>
        /// <returns>是否存在</returns>
        internal bool HasKey(string key)
        {
            EnsureProvider();

            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            // 检查当前语言
            if (_localizationProvider.HasKey(_currentLanguage, key))
            {
                return true;
            }

            // 检查默认语言
            if (_currentLanguage != _defaultLanguage)
            {
                return _localizationProvider.HasKey(_defaultLanguage, key);
            }

            return false;
        }

        /// <summary>
        /// 获取所有键（业务层：null表示当前语言）
        /// </summary>
        /// <param name="languageCode">语言代码（null表示当前语言）</param>
        /// <returns>键列表</returns>
        internal IReadOnlyList<string> GetAllKeys(string languageCode = null)
        {
            EnsureProvider();
            var lang = languageCode ?? _currentLanguage;
            return _localizationProvider.GetAllKeys(lang);
        }

        #endregion

        #region 私有辅助方法

        private void EnsureProvider()
        {
            if (_localizationProvider == null)
            {
                throw new InvalidOperationException($"[{Name}] LocalizationProvider未初始化");
            }
        }

        /// <summary>
        /// 初始化支持的语言列表（业务规则）
        /// </summary>
        private void InitializeSupportedLanguages()
        {
            _supportedLanguages.Clear();
            _supportedLanguages.Add("CN");
            _supportedLanguages.Add("US");
            // 可以扩展更多语言
        }

        /// <summary>
        /// 检测系统语言（业务规则）
        /// </summary>
        private string DetectSystemLanguage()
        {
            var systemLanguage = Application.systemLanguage;
            return MapSystemLanguage(systemLanguage);
        }

        /// <summary>
        /// 映射Unity系统语言到语言代码（业务规则）
        /// </summary>
        private string MapSystemLanguage(SystemLanguage systemLanguage)
        {
            return systemLanguage switch
            {
                SystemLanguage.Chinese => "CN",
                SystemLanguage.ChineseSimplified => "CN",
                SystemLanguage.ChineseTraditional => "TW",
                SystemLanguage.English => "US",
                SystemLanguage.Japanese => "JP",
                SystemLanguage.Korean => "KR",
                SystemLanguage.French => "FR",
                SystemLanguage.German => "DE",
                SystemLanguage.Spanish => "ES",
                SystemLanguage.Portuguese => "BR",
                SystemLanguage.Russian => "RU",
                _ => _defaultLanguage
            };
        }

        #endregion

        protected override UniTask OnShutdownAsync()
        {
            _supportedLanguages.Clear();
            _localizationProvider = null;
            return base.OnShutdownAsync();
        }
    }
}

