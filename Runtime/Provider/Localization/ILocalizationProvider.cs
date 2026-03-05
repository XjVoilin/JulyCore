using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;

namespace JulyCore.Provider.Localization
{
    /// <summary>
    /// 多语言提供者接口
    /// 纯技术执行层：负责资源加载、数据存储、数据检索
    /// 不包含任何业务语义，不维护业务状态
    /// 所有业务逻辑由Module层处理
    /// </summary>
    public interface ILocalizationProvider : IProvider
    {
        #region 语言包加载（纯技术操作）

        /// <summary>
        /// 加载语言包（通过资源文件名，负责资源加载）
        /// </summary>
        /// <param name="languageCode">语言代码</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否加载成功</returns>
        UniTask<bool> LoadLanguageAsync(string languageCode, CancellationToken cancellationToken = default);

        /// <summary>
        /// 卸载语言包
        /// </summary>
        /// <param name="languageCode">语言代码</param>
        void UnloadLanguage(string languageCode);

        /// <summary>
        /// 检查语言包是否已加载
        /// </summary>
        /// <param name="languageCode">语言代码</param>
        /// <returns>是否已加载</returns>
        bool IsLanguageLoaded(string languageCode);

        #endregion

        #region 数据检索（基于语言代码的纯技术操作）

        /// <summary>
        /// 获取本地化文本（指定语言代码）
        /// </summary>
        /// <param name="languageCode">语言代码</param>
        /// <param name="key">文本键</param>
        /// <param name="defaultValue">默认值（如果key不存在）</param>
        /// <returns>本地化文本</returns>
        string GetText(string languageCode, string key, string defaultValue = null);

        /// <summary>
        /// 检查键是否存在（指定语言代码）
        /// </summary>
        /// <param name="languageCode">语言代码</param>
        /// <param name="key">文本键</param>
        /// <returns>是否存在</returns>
        bool HasKey(string languageCode, string key);

        /// <summary>
        /// 获取所有键（指定语言代码）
        /// </summary>
        /// <param name="languageCode">语言代码</param>
        /// <returns>键列表</returns>
        IReadOnlyList<string> GetAllKeys(string languageCode);

        #endregion
    }
}

