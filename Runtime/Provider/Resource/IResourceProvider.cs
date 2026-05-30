using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace JulyCore.Provider.Resource
{
    /// <summary>
    /// 资源提供者接口
    /// 提供资源加载/场景加载的技术能力
    /// 引用计数完全委托底层（如 YooAsset），句柄释放即减少底层计数
    /// </summary>
    public interface IResourceProvider : Core.IProvider
    {
        /// <summary>
        /// 异步加载资源，返回句柄。
        /// 句柄 Dispose / BindTo / scope 释放时减少底层引用计数。
        /// 加载失败返回 null。
        /// </summary>
        UniTask<ResourceHandle<T>> LoadAssetAsync<T>(string fileName, CancellationToken cancellationToken = default) where T : Object;

        /// <summary>
        /// 按 Tag 下载资源（含整体重试）。单次下载内部由 YooAsset 处理单文件重试，
        /// 此方法在整体失败时按递增延迟重试整个下载批次。
        /// </summary>
        UniTask<bool> DownloadByTagWithRetryAsync(string tag, int maxRetries = 3, CancellationToken ct = default);

        /// <summary>
        /// 检查资源是否存在
        /// </summary>
        bool HasAsset(string fileName);

        #region 场景加载

        /// <summary>
        /// 异步加载场景
        /// </summary>
        UniTask<UnityEngine.SceneManagement.Scene> LoadSceneAsync(
            string sceneName,
            UnityEngine.SceneManagement.LoadSceneMode loadSceneMode = UnityEngine.SceneManagement.LoadSceneMode.Single,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步卸载场景
        /// </summary>
        UniTask<bool> UnloadSceneAsync(string sceneName, CancellationToken cancellationToken = default);

        #endregion
    }
}
