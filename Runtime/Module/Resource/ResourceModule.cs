using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Module.Base;
using JulyCore.Provider.Resource;
using UnityEngine;

namespace JulyCore.Module.Resource
{
    /// <summary>
    /// 资源模块
    /// 
    /// 在 IResourceProvider 的基础上提供便捷 API：
    /// - Handle 模式：句柄释放时减少底层引用计数，支持 using / GameObject 绑定 / scope 管理
    /// - Scoped 模式：加载即用完即释放（配置、DLL 等一次性读取场景）
    /// </summary>
    internal class ResourceModule : ModuleBase
    {
        private IResourceProvider _resourceProvider;

        protected override LogChannel LogChannel => LogChannel.Resource;
        public override int Priority => Frameworkconst.PriorityResourceModule;

        protected override UniTask OnInitAsync()
        {
            try
            {
                _resourceProvider = GetProvider<IResourceProvider>();
                return base.OnInitAsync();
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 资源模块初始化失败: {ex.Message}");
                throw;
            }
        }

        #region 核心加载

        /// <summary>
        /// 异步加载资源并返回句柄。
        /// 句柄释放时自动减少引用计数，支持 using 语句和 GameObject 绑定。
        /// </summary>
        internal UniTask<ResourceHandle<T>> LoadWithHandleAsync<T>(string fileName, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            EnsureProvider();
            return _resourceProvider.LoadAssetAsync<T>(fileName, cancellationToken);
        }

        /// <summary>
        /// 加载资源、在回调内取值后立即释放。
        /// 适用于配置、DLL 字节等"读取一次即丢弃"的场景。
        /// 加载失败返回 default(TResult)。
        /// </summary>
        internal async UniTask<TResult> LoadScopedAsync<T, TResult>(string fileName, Func<T, TResult> use, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            EnsureProvider();
            using var handle = await _resourceProvider.LoadAssetAsync<T>(fileName, cancellationToken);
            if (handle == null || !handle.IsValid)
            {
                return default;
            }
            return use(handle.Asset);
        }

        /// <summary>
        /// 加载资源并绑定到 GameObject 的生命周期，返回资源对象。
        /// GameObject 销毁时自动释放底层引用。
        /// </summary>
        internal async UniTask<T> LoadAsync<T>(string fileName, GameObject bindTo, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            EnsureProvider();
            var handle = await _resourceProvider.LoadAssetAsync<T>(fileName, cancellationToken);
            if (handle == null || !handle.IsValid)
            {
                handle?.Dispose();
                return null;
            }
            handle.BindTo(bindTo);
            return handle.Asset;
        }

        /// <summary>
        /// 加载 Prefab 并实例化到指定父节点，实例销毁时自动释放底层引用。
        /// </summary>
        internal async UniTask<GameObject> InstantiateAsync(string fileName, Transform parent = null, CancellationToken cancellationToken = default)
        {
            EnsureProvider();
            var handle = await _resourceProvider.LoadAssetAsync<GameObject>(fileName, cancellationToken);
            if (handle == null || !handle.IsValid)
            {
                handle?.Dispose();
                return null;
            }
            var instance = UnityEngine.Object.Instantiate(handle.Asset, parent);
            handle.BindTo(instance);
            return instance;
        }

        /// <summary>
        /// 加载 Prefab、实例化并返回指定组件，实例销毁时自动释放底层引用。
        /// </summary>
        internal async UniTask<T> InstantiateAsync<T>(string fileName, Transform parent = null, CancellationToken cancellationToken = default) where T : Component
        {
            var instance = await InstantiateAsync(fileName, parent, cancellationToken);
            if (instance == null) return null;
            var component = instance.GetComponent<T>();
            if (component == null)
            {
                JLogger.LogWarning($"[ResourceModule] Prefab '{fileName}' 上未找到组件 {typeof(T).Name}，销毁实例");
                UnityEngine.Object.Destroy(instance);
            }
            return component;
        }

        /// <summary>
        /// 批量并行加载资源并返回句柄数组。
        /// 任一资源加载失败或取消时，自动释放所有已完成的句柄，保证不泄漏。
        /// </summary>
        internal async UniTask<ResourceHandle<T>[]> LoadBatchAsync<T>(IReadOnlyList<string> fileNames, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            EnsureProvider();
            if (fileNames == null || fileNames.Count == 0)
                return Array.Empty<ResourceHandle<T>>();

            var handles = new ResourceHandle<T>[fileNames.Count];
            try
            {
                var tasks = new UniTask<ResourceHandle<T>>[fileNames.Count];
                for (int i = 0; i < fileNames.Count; i++)
                    tasks[i] = _resourceProvider.LoadAssetAsync<T>(fileNames[i], cancellationToken);

                var results = await UniTask.WhenAll(tasks);
                for (int i = 0; i < results.Length; i++)
                    handles[i] = results[i];

                return handles;
            }
            catch
            {
                for (int i = 0; i < handles.Length; i++)
                    handles[i]?.Dispose();
                throw;
            }
        }

        #endregion

        #region 下载

        public UniTask<bool> DownloadByTagWithRetryAsync(string tag, int maxRetries = 3, CancellationToken ct = default)
        {
            EnsureProvider();
            return _resourceProvider.DownloadByTagWithRetryAsync(tag, maxRetries, ct);
        }

        #endregion

        #region 检查

        internal bool HasAsset(string fileName)
        {
            return _resourceProvider?.HasAsset(fileName) ?? false;
        }

        #endregion

        private void EnsureProvider()
        {
            if (_resourceProvider == null)
                throw new InvalidOperationException($"[{Name}] ResourceProvider未初始化，请确保 Module 已完成初始化后再调用 GF.Resource");
        }

        protected override void OnShutdown()
        {
            _resourceProvider = null;
        }
    }
}
