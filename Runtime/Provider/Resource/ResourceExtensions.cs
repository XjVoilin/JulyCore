using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using UnityEngine;

namespace JulyCore.Provider.Resource
{
    /// <summary>
    /// 资源加载扩展方法
    /// 简化资源加载和自动生命周期绑定
    /// 
    /// 【设计理念】
    /// 通过扩展方法让任何 Component/MonoBehaviour 都可以方便地加载资源
    /// 并自动将资源生命周期绑定到组件，组件销毁时自动释放资源
    /// </summary>
    public static class ResourceExtensions
    {
        /// <summary>
        /// 加载资源并自动绑定到当前组件的生命周期
        /// 组件销毁时自动释放资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="component">绑定目标组件</param>
        /// <param name="fileName">资源文件名</param>
        /// <param name="captureStackTrace">是否捕获调用栈（用于泄漏调试）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>资源句柄（资源通过 handle.Asset 访问）</returns>
        /// <example>
        /// // 在 MonoBehaviour 中使用
        /// var handle = await this.LoadResourceAsync&lt;Sprite&gt;("icons/player");
        /// _image.sprite = handle.Asset;
        /// // 无需手动释放，组件销毁时自动释放
        /// </example>
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

            var provider = GetResourceProvider();
            if (provider == null)
            {
                JLogger.LogError("[ResourceExtensions] 未找到 IResourceProvider，请确保框架已初始化");
                return null;
            }

            var handle = await provider.LoadWithHandleAsync<T>(fileName, captureStackTrace, cancellationToken);
            if (handle != null)
            {
                handle.BindTo(component);
            }

            return handle;
        }

        /// <summary>
        /// 加载资源并自动绑定到 GameObject 的生命周期
        /// GameObject 销毁时自动释放资源
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

            var provider = GetResourceProvider();
            if (provider == null)
            {
                JLogger.LogError("[ResourceExtensions] 未找到 IResourceProvider，请确保框架已初始化");
                return null;
            }

            var handle = await provider.LoadWithHandleAsync<T>(fileName, captureStackTrace, cancellationToken);
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

        /// <summary>
        /// 获取资源提供者
        /// </summary>
        private static IResourceProvider GetResourceProvider()
        {
            try
            {
                return FrameworkContext.Instance?.Container?.TryResolve<IResourceProvider>(out var provider) == true 
                    ? provider 
                    : null;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// MonoBehaviour 资源管理扩展基类
    /// 继承此类可以更方便地管理资源生命周期
    /// </summary>
    public abstract class ResourceManagedBehaviour : MonoBehaviour
    {
        /// <summary>
        /// 加载资源并自动绑定到当前组件
        /// </summary>
        protected UniTask<ResourceHandle<T>> LoadResourceAsync<T>(
            string fileName,
            bool captureStackTrace = false,
            CancellationToken cancellationToken = default) where T : Object
        {
            return ResourceExtensions.LoadResourceAsync<T>(this, fileName, captureStackTrace, cancellationToken);
        }

        /// <summary>
        /// 批量加载资源并自动绑定
        /// </summary>
        protected UniTask<ResourceHandle<T>[]> LoadResourcesAsync<T>(
            string[] fileNames,
            bool captureStackTrace = false,
            CancellationToken cancellationToken = default) where T : Object
        {
            return ResourceExtensions.LoadResourcesAsync<T>(this, fileNames, captureStackTrace, cancellationToken);
        }
    }
}

