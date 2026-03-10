using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using UnityEngine;
using UnityEngine.UI;

namespace JulyCore.Provider.Resource
{
    /// <summary>
    /// 资源加载扩展方法
    /// 通过扩展方法让任何 Component/GameObject 都可以方便地加载资源
    /// 并自动将资源生命周期绑定到组件，组件销毁时自动释放资源
    /// </summary>
    public static class ResourceExtensions
    {
        #region Image — 加载 Sprite 并设置

        /// <summary>
        /// 加载 Sprite 并设置到 Image（fire-and-forget）
        /// 资源生命周期自动绑定到 Image 所在的 GameObject
        /// </summary>
        public static void LoadImage(this Image img, string resName)
        {
            LoadImageAsync(img, resName).Forget();
        }

        /// <summary>
        /// 加载 Sprite 并设置到 Image（可等待版本）
        /// </summary>
        /// <returns>资源句柄，可用于手动管理；加载失败返回 null</returns>
        public static async UniTask<ResourceHandle<Sprite>> LoadImageAsync(this Image img, string resName,
            CancellationToken cancellationToken = default)
        {
            if (img == null) return null;

            var handle = await LoadResourceAsync<Sprite>(img, resName, false, cancellationToken);
            if (handle != null && handle.IsValid)
            {
                img.overrideSprite = handle.Asset;
            }

            return handle;
        }

        #endregion

        #region SpriteRenderer — 加载 Sprite 并设置

        /// <summary>
        /// 加载 Sprite 并设置到 SpriteRenderer（fire-and-forget）
        /// </summary>
        public static void LoadSprite(this SpriteRenderer renderer, string resName)
        {
            LoadSpriteAsync(renderer, resName).Forget();
        }

        /// <summary>
        /// 加载 Sprite 并设置到 SpriteRenderer（可等待版本）
        /// </summary>
        public static async UniTask<ResourceHandle<Sprite>> LoadSpriteAsync(this SpriteRenderer renderer,
            string resName, CancellationToken cancellationToken = default)
        {
            if (renderer == null) return null;

            var handle = await LoadResourceAsync<Sprite>(renderer, resName, false, cancellationToken);
            if (handle != null && handle.IsValid)
            {
                renderer.sprite = handle.Asset;
            }

            return handle;
        }

        #endregion

        #region RawImage — 加载 Texture 并设置

        /// <summary>
        /// 加载 Texture 并设置到 RawImage（fire-and-forget）
        /// </summary>
        public static void LoadTexture(this RawImage img, string resName)
        {
            LoadTextureAsync(img, resName).Forget();
        }

        /// <summary>
        /// 加载 Texture 并设置到 RawImage（可等待版本）
        /// </summary>
        public static async UniTask<ResourceHandle<Texture>> LoadTextureAsync(this RawImage img, string resName,
            CancellationToken cancellationToken = default)
        {
            if (img == null) return null;

            var handle = await LoadResourceAsync<Texture>(img, resName, false, cancellationToken);
            if (handle != null && handle.IsValid)
            {
                img.texture = handle.Asset;
            }

            return handle;
        }

        #endregion

        #region 通用资源加载（底层方法）

        /// <summary>
        /// 加载资源并自动绑定到当前组件的生命周期
        /// 组件销毁时自动释放资源
        /// </summary>
        public static async UniTask<ResourceHandle<T>> LoadResourceAsync<T>(
            this UnityEngine.Component component,
            string fileName,
            bool captureStackTrace = false,
            CancellationToken cancellationToken = default) where T : Object
        {
            if (component == null)
            {
                JLogger.LogWarning("[ResourceExtensions] 组件为空，无法加载资源");
                return null;
            }

            var handle = await GF.Resource.LoadWithHandleAsync<T>(fileName, captureStackTrace, cancellationToken);
            if (handle != null)
            {
                handle.BindTo(component);
            }

            return handle;
        }

        /// <summary>
        /// 加载资源并自动绑定到 GameObject 的生命周期
        /// </summary>
        public static async UniTask<ResourceHandle<T>> LoadResourceAsync<T>(
            this GameObject gameObject,
            string fileName,
            bool captureStackTrace = false,
            CancellationToken cancellationToken = default) where T : Object
        {
            if (gameObject == null)
            {
                JLogger.LogWarning("[ResourceExtensions] GameObject 为空，无法加载资源");
                return null;
            }

            var handle = await GF.Resource.LoadWithHandleAsync<T>(fileName, captureStackTrace, cancellationToken);
            if (handle != null)
            {
                handle.BindTo(gameObject);
            }

            return handle;
        }

        /// <summary>
        /// 批量加载资源并绑定到组件
        /// </summary>
        public static async UniTask<ResourceHandle<T>[]> LoadResourcesAsync<T>(
            this UnityEngine.Component component,
            string[] fileNames,
            bool captureStackTrace = false,
            CancellationToken cancellationToken = default) where T : Object
        {
            if (component == null || fileNames == null || fileNames.Length == 0)
            {
                return System.Array.Empty<ResourceHandle<T>>();
            }

            var handles = new ResourceHandle<T>[fileNames.Length];
            for (int i = 0; i < fileNames.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                handles[i] = await component.LoadResourceAsync<T>(fileNames[i], captureStackTrace, cancellationToken);
            }

            return handles;
        }

        #endregion
    }
}
