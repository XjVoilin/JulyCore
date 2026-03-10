using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace JulyCore.Provider.Resource
{
    /// <summary>
    /// 资源提供者接口
    /// 提供资源加载/卸载的技术能力
    /// 便捷 API（Handle 自动管理、批量加载）由 ResourceModule 在此基础上构建
    /// </summary>
    public interface IResourceProvider : Core.IProvider
    {
        /// <summary>
        /// 异步加载资源（手动管理引用计数）
        /// 使用引用计数管理，多次加载同一资源会增加引用计数
        /// 注意：必须配对调用 Unload，否则会导致资源泄漏
        /// </summary>
        UniTask<T> LoadAsync<T>(string fileName, CancellationToken cancellationToken = default) where T : Object;

        /// <summary>
        /// 预加载资源（不增加引用计数，仅将资源加载到内存）
        /// </summary>
        UniTask<bool> PreloadAsync<T>(string fileName, CancellationToken cancellationToken = default) where T : Object;

        /// <summary>
        /// 加载子资源（如SpriteAtlas中的Sprite、AudioClip等）
        /// </summary>
        UniTask<T> LoadSubAssetAsync<T>(string fileName, string assetName, CancellationToken cancellationToken = default) where T : Object;

        /// <summary>
        /// 加载所有子资源
        /// </summary>
        UniTask<List<T>> LoadAllSubAssetsAsync<T>(string fileName, CancellationToken cancellationToken = default) where T : Object;

        /// <summary>
        /// 检查资源是否存在
        /// </summary>
        bool HasAsset(string fileName);

        /// <summary>
        /// 卸载资源（减少引用计数，计数为0时真正释放）
        /// </summary>
        void Unload(Object obj);

        /// <summary>
        /// 卸载所有资源
        /// </summary>
        void UnloadAll();

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
