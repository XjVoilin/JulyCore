using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Provider.Base;
using JulyCore.Provider.Data;
using JulyCore.Provider.Resource;
using UnityEngine;

namespace JulyCore.Provider.Localization
{
    /// <summary>
    /// Unity多语言提供者实现
    /// 纯技术执行层：负责资源加载、数据存储、数据检索
    /// 不包含任何业务语义，不维护业务状态
    /// </summary>
    internal class LocalizationProvider : ProviderBase, ILocalizationProvider
    {
        public override int Priority => Frameworkconst.PriorityLocalizationProvider;
        protected override LogChannel LogChannel => LogChannel.Localization;

        private readonly IResourceProvider _resourceProvider;
        private readonly ISerializeProvider _serializeProvider;

        // 语言数据：语言代码 -> (键 -> 值)
        private readonly Dictionary<string, Dictionary<string, string>> _languageDataDic = new();

        /// <summary>
        /// 构造函数（依赖通过 DI 容器注入）
        /// </summary>
        public LocalizationProvider(IResourceProvider resourceProvider, ISerializeProvider serializeProvider)
        {
            _resourceProvider = resourceProvider ?? throw new ArgumentNullException(nameof(resourceProvider));
            _serializeProvider = serializeProvider ?? throw new ArgumentNullException(nameof(serializeProvider));
        }

        #region ILocalizationProvider 实现

        public async UniTask<bool> LoadLanguageAsync(string languageCode, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(languageCode))
            {
                return false;
            }

            // 如果已加载，直接返回
            if (_languageDataDic.ContainsKey(languageCode))
            {
                return true;
            }

            try
            {
                // 尝试从资源加载语言包
                var textAsset =
                    await _resourceProvider.LoadAsync<TextAsset>(GetLanguageFileName(languageCode), cancellationToken);
                if (textAsset != null)
                {
                    var data = _serializeProvider.Deserialize<Dictionary<string, string>>(Encoding.UTF8.GetBytes(textAsset.text));
                    _languageDataDic[languageCode] = data;
                    _resourceProvider.Unload(textAsset);
                    Log($"[{Name}] 语言包加载成功: {languageCode}");
                    return true;
                }

                // 加载失败，创建空字典
                if (!_languageDataDic.ContainsKey(languageCode))
                {
                    _languageDataDic[languageCode] = new Dictionary<string, string>();
                }

                LogWarning($"[{Name}] 语言包加载失败，使用空数据: {languageCode}");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 加载语言包异常 ({languageCode}): {ex.Message}");
                return false;
            }
        }

        public void UnloadLanguage(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode))
            {
                return;
            }

            if (_languageDataDic.Remove(languageCode))
            {
                Log($"[{Name}] 语言包已卸载: {languageCode}");
            }
        }

        public bool IsLanguageLoaded(string languageCode)
        {
            return !string.IsNullOrEmpty(languageCode) && _languageDataDic.ContainsKey(languageCode);
        }

        public string GetText(string languageCode, string key, string defaultValue = null)
        {
            if (string.IsNullOrEmpty(languageCode) || string.IsNullOrEmpty(key))
            {
                return defaultValue ?? string.Empty;
            }

            if (_languageDataDic.TryGetValue(languageCode, out var dict) &&
                dict.TryGetValue(key, out var value))
            {
                return value;
            }

            return defaultValue ?? key;
        }

        public bool HasKey(string languageCode, string key)
        {
            if (string.IsNullOrEmpty(languageCode) || string.IsNullOrEmpty(key))
            {
                return false;
            }

            if (_languageDataDic.TryGetValue(languageCode, out var dict))
            {
                return dict.ContainsKey(key);
            }

            return false;
        }

        public IReadOnlyList<string> GetAllKeys(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode))
            {
                return Array.Empty<string>();
            }

            if (_languageDataDic.TryGetValue(languageCode, out var dict))
            {
                return dict.Keys.ToList();
            }

            return Array.Empty<string>();
        }

        #endregion

        #region 私有辅助方法

        private string GetLanguageFileName(string languageCode)
        {
            return $"Language{languageCode}";
        }

        #endregion

        protected override UniTask OnShutdownAsync()
        {
            _languageDataDic.Clear();
            Log($"[{Name}] 多语言提供者已关闭");
            return UniTask.CompletedTask;
        }
    }
}
