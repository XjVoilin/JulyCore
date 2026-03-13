using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Module.Resource;
using JulyCore.Provider.Resource;

namespace JulyCore
{
    public static partial class GF
    {
        /// <summary>
        /// 资源相关操作
        /// </summary>
        public static class Resource
        {
            private static ResourceModule _module;

            private static ResourceModule Module
            {
                get
                {
                    _module ??= GetModule<ResourceModule>();
                    return _module;
                }
            }

            #region 核心加载

            /// <summary>
            /// 异步加载资源
            /// </summary>
            public static UniTask<T> LoadAsync<T>(string fileName, CancellationToken cancellationToken = default)
                where T : UnityEngine.Object
            {
                return Module.LoadAsync<T>(fileName, cancellationToken);
            }

            /// <summary>
            /// 异步加载资源并返回句柄（自动管理引用计数）
            /// 
            /// 【使用示例】
            /// // 方式1：using 语句（最安全）
            /// using (var handle = await GF.Resource.LoadWithHandleAsync&lt;Sprite&gt;("icon"))
            /// {
            ///     image.sprite = handle.Asset;
            /// }
            /// 
            /// // 方式2：绑定到 GameObject（UI 场景推荐）
            /// var handle = await GF.Resource.LoadWithHandleAsync&lt;Sprite&gt;("icon");
            /// handle.BindTo(gameObject);
            /// image.sprite = handle.Asset;
            /// </summary>
            public static UniTask<ResourceHandle<T>> LoadWithHandleAsync<T>(string fileName,
                bool captureStackTrace = false, CancellationToken cancellationToken = default)
                where T : UnityEngine.Object
            {
                return Module.LoadWithHandleAsync<T>(fileName, captureStackTrace, cancellationToken);
            }

            #endregion

            #region 批量与预加载

            /// <summary>
            /// 预加载资源（不增加引用计数，仅将资源加载到内存）
            /// </summary>
            public static UniTask<bool> PreloadAsync<T>(string fileName, CancellationToken cancellationToken = default)
                where T : UnityEngine.Object
            {
                return Module.PreloadAsync<T>(fileName, cancellationToken);
            }

            /// <summary>
            /// 批量加载资源
            /// </summary>
            public static UniTask<List<T>> LoadBatchAsync<T>(IEnumerable<string> fileNames,
                CancellationToken cancellationToken = default) where T : UnityEngine.Object
            {
                return Module.LoadBatchAsync<T>(fileNames, cancellationToken);
            }

            #endregion

            #region 下载

            /// <summary>
            /// 批量加载资源
            /// </summary>
            public static UniTask<bool> DownloadByTagWithRetryAsync(string tag, int maxRetries = 3,
                CancellationToken ct = default)
            {
                return Module.DownloadByTagWithRetryAsync(tag, maxRetries, ct);
            }

            #endregion

            #region 子资源加载

            /// <summary>
            /// 加载子资源（如SpriteAtlas中的Sprite、AudioClip等）
            /// </summary>
            public static UniTask<T> LoadSubAssetAsync<T>(string fileName, string assetName,
                CancellationToken cancellationToken = default) where T : UnityEngine.Object
            {
                return Module.LoadSubAssetAsync<T>(fileName, assetName, cancellationToken);
            }

            /// <summary>
            /// 加载所有子资源
            /// </summary>
            public static UniTask<List<T>> LoadAllSubAssetsAsync<T>(string fileName,
                CancellationToken cancellationToken = default) where T : UnityEngine.Object
            {
                return Module.LoadAllSubAssetsAsync<T>(fileName, cancellationToken);
            }

            #endregion

            #region 检查与卸载

            /// <summary>
            /// 检查资源是否存在
            /// </summary>
            public static bool HasAsset(string fileName)
            {
                return Module.HasAsset(fileName);
            }

            /// <summary>
            /// 卸载资源（减少引用计数，建议使用 Handle 模式代替手动卸载）
            /// </summary>
            public static void Unload(UnityEngine.Object obj)
            {
                if (obj == null) return;
                Module.Unload(obj);
            }

            /// <summary>
            /// 卸载所有资源
            /// </summary>
            public static void UnloadAll()
            {
                Module.UnloadAll();
            }

            #endregion
        }
    }
}