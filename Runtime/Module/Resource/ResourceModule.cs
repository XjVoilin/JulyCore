using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Module.Base;
using JulyCore.Provider.Resource;

namespace JulyCore.Module.Resource
{
    /// <summary>
    /// 资源模块
    /// 
    /// 在 IResourceProvider 的基础上提供便捷 API：
    /// - Handle 模式：自动管理引用计数，绑定 GameObject 生命周期
    /// - 批量加载：对 Provider 的 LoadAsync 进行循环调用
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

        internal UniTask<T> LoadAsync<T>(string fileName, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            EnsureProvider();
            return _resourceProvider.LoadAsync<T>(fileName, cancellationToken);
        }

        /// <summary>
        /// 异步加载资源并返回句柄（自动管理引用计数）
        /// 句柄释放时自动减少引用计数，支持 using 语句和 GameObject 绑定
        /// </summary>
        internal async UniTask<ResourceHandle<T>> LoadWithHandleAsync<T>(string fileName, bool captureStackTrace = false, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            EnsureProvider();
            var asset = await _resourceProvider.LoadAsync<T>(fileName, cancellationToken);
            if (asset == null)
            {
                return null;
            }
            return new ResourceHandle<T>(asset, fileName, _resourceProvider, captureStackTrace);
        }

        #endregion

        #region 批量与预加载

        internal UniTask<bool> PreloadAsync<T>(string fileName, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            EnsureProvider();
            return _resourceProvider.PreloadAsync<T>(fileName, cancellationToken);
        }

        internal async UniTask<List<T>> LoadBatchAsync<T>(IEnumerable<string> fileNames, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            EnsureProvider();
            var results = new List<T>();
            foreach (var fileName in fileNames)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                var resource = await _resourceProvider.LoadAsync<T>(fileName, cancellationToken);
                results.Add(resource);
            }
            return results;
        }

        #endregion

        #region 下载

        public UniTask<bool> DownloadByTagWithRetryAsync(string tag, int maxRetries = 3, CancellationToken ct = default)
        {
            EnsureProvider();
            return _resourceProvider.DownloadByTagWithRetryAsync(tag, maxRetries, ct);
        }

        #endregion

        #region 子资源加载

        internal UniTask<T> LoadSubAssetAsync<T>(string fileName, string assetName, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            EnsureProvider();
            return _resourceProvider.LoadSubAssetAsync<T>(fileName, assetName, cancellationToken);
        }

        internal UniTask<List<T>> LoadAllSubAssetsAsync<T>(string fileName, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            EnsureProvider();
            return _resourceProvider.LoadAllSubAssetsAsync<T>(fileName, cancellationToken);
        }

        #endregion

        #region 检查与卸载

        internal bool HasAsset(string fileName)
        {
            return _resourceProvider?.HasAsset(fileName) ?? false;
        }

        internal void Unload(UnityEngine.Object obj)
        {
            if (obj == null) return;
            _resourceProvider?.Unload(obj);
        }

        internal void UnloadAll()
        {
            _resourceProvider?.UnloadAll();
        }

        #endregion

        private void EnsureProvider()
        {
            if (_resourceProvider == null)
                throw new InvalidOperationException($"[{Name}] ResourceProvider未初始化，请确保 Module 已完成初始化后再调用 GF.Resource");
        }

        protected override UniTask OnShutdownAsync()
        {
            _resourceProvider = null;
            return base.OnShutdownAsync();
        }
    }
}
