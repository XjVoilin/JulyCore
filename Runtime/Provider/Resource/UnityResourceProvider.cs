using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Provider.Base;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JulyCore.Provider.Resource
{
    /// <summary>
    /// Unity Resources 资源提供者实现
    /// 使用 Unity 内置的 Resources.Load 进行资源加载
    /// 作为框架的默认资源提供者，适用于简单项目或原型开发
    /// 
    /// 生产环境建议使用 YooAssetResourceProvider 或其他 AssetBundle 方案
    /// </summary>
    public class UnityResourceProvider : ProviderBase, IResourceProvider
    {
        public override int Priority => Frameworkconst.PriorityResourceProvider;
        protected override LogChannel LogChannel => LogChannel.Resource;

        private readonly ConcurrentDictionary<UnityEngine.Object, int> _refCounts = new();
        private readonly ConcurrentDictionary<string, UnityEngine.Object> _pathToResourceCache = new();
        private readonly ConcurrentDictionary<UnityEngine.Object, string> _objectToPath = new();
        private readonly ConcurrentDictionary<string, UnityEngine.Object> _preloadedResources = new();

        protected override UniTask OnInitAsync()
        {
            return UniTask.CompletedTask;
        }

        public async UniTask<T> LoadAsync<T>(string fileName, CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(fileName))
            {
                LogWarning($"[{Name}] 资源路径不能为空");
                return null;
            }

            var path = NormalizePath(fileName);

            if (TryGetCachedResource<T>(path, out var cachedResource))
            {
                IncrementRefCount(cachedResource);
                return cachedResource;
            }

            if (_preloadedResources.TryRemove(path, out var preloadedObj) && preloadedObj is T preloaded)
            {
                RecordResourceMapping(path, preloaded);
                IncrementRefCount(preloaded);
                return preloaded;
            }

            try
            {
                var request = Resources.LoadAsync<T>(path);
                await request.ToUniTask(cancellationToken: cancellationToken);

                if (request.asset == null)
                {
                    LogWarning($"[{Name}] 资源加载失败: {path}");
                    return null;
                }

                var resource = request.asset as T;
                if (resource == null)
                {
                    LogWarning($"[{Name}] 资源类型不匹配: {path}");
                    return null;
                }

                RecordResourceMapping(path, resource);
                IncrementRefCount(resource);
                return resource;
            }
            catch (OperationCanceledException)
            {
                LogWarning($"[{Name}] 资源加载已取消: {path}");
                return null;
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 加载资源异常: {path}, 错误: {ex.Message}");
                return null;
            }
        }

        public async UniTask<bool> PreloadAsync<T>(string fileName, CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            var path = NormalizePath(fileName);

            if (_preloadedResources.ContainsKey(path) || _pathToResourceCache.ContainsKey(path))
            {
                return true;
            }

            try
            {
                var request = Resources.LoadAsync<T>(path);
                await request.ToUniTask(cancellationToken: cancellationToken);

                if (request.asset != null)
                {
                    _preloadedResources[path] = request.asset;
                    return true;
                }

                return false;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 预加载资源异常: {path}, 错误: {ex.Message}");
                return false;
            }
        }

        public async UniTask<T> LoadSubAssetAsync<T>(string fileName, string assetName, CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(assetName))
            {
                LogWarning($"[{Name}] 子资源参数不能为空");
                return null;
            }

            var path = NormalizePath(fileName);

            try
            {
                var allAssets = Resources.LoadAll<T>(path);
                await UniTask.Yield(cancellationToken);

                foreach (var asset in allAssets)
                {
                    if (asset.name == assetName)
                    {
                        return asset;
                    }
                }

                LogWarning($"[{Name}] 未找到子资源: {path}/{assetName}");
                return null;
            }
            catch (OperationCanceledException)
            {
                LogWarning($"[{Name}] 加载子资源已取消: {path}/{assetName}");
                return null;
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 加载子资源异常: {path}/{assetName}, 错误: {ex.Message}");
                return null;
            }
        }

        public async UniTask<List<T>> LoadAllSubAssetsAsync<T>(string fileName, CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return new List<T>();
            }

            var path = NormalizePath(fileName);

            try
            {
                var allAssets = Resources.LoadAll<T>(path);
                await UniTask.Yield(cancellationToken);
                return new List<T>(allAssets);
            }
            catch (OperationCanceledException)
            {
                LogWarning($"[{Name}] 加载所有子资源已取消: {path}");
                return new List<T>();
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 加载所有子资源异常: {path}, 错误: {ex.Message}");
                return new List<T>();
            }
        }

        public bool HasAsset(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            var path = NormalizePath(fileName);

            if (_pathToResourceCache.ContainsKey(path) || _preloadedResources.ContainsKey(path))
            {
                return true;
            }

            var resource = Resources.Load(path);
            if (resource != null)
            {
                Resources.UnloadAsset(resource);
                return true;
            }

            return false;
        }

        public void Unload(UnityEngine.Object obj)
        {
            if (obj == null)
            {
                return;
            }

            try
            {
                if (!_objectToPath.TryGetValue(obj, out var path))
                {
                    LogWarning($"[{Name}] 未找到资源对象: {obj.name}");
                    return;
                }

                if (DecrementRefCount(obj))
                {
                    CleanupResourceMappings(path);

                    if (!(obj is GameObject))
                    {
                        Resources.UnloadAsset(obj);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 卸载资源异常: {ex.Message}");
            }
        }

        public void UnloadAll()
        {
            try
            {
                _pathToResourceCache.Clear();
                _objectToPath.Clear();
                _refCounts.Clear();
                _preloadedResources.Clear();

                Resources.UnloadUnusedAssets();
                GC.Collect();
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 卸载所有资源异常: {ex.Message}");
            }
        }

        #region 场景加载

        public async UniTask<UnityEngine.SceneManagement.Scene> LoadSceneAsync(
            string sceneName,
            LoadSceneMode loadSceneMode = LoadSceneMode.Single,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                throw new ArgumentException("场景名称不能为空", nameof(sceneName));
            }

            var existingScene = SceneManager.GetSceneByName(sceneName);
            if (existingScene.IsValid() && existingScene.isLoaded)
            {
                LogWarning($"[{Name}] 场景 {sceneName} 已加载，直接返回");
                return existingScene;
            }

            var asyncOperation = SceneManager.LoadSceneAsync(sceneName, loadSceneMode);
            if (asyncOperation == null)
            {
                throw new JulyException($"[{Name}] 场景 {sceneName} 加载失败（场景不存在或路径错误）");
            }

            asyncOperation.allowSceneActivation = true;

            while (!asyncOperation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    LogWarning($"[{Name}] 场景 {sceneName} 加载被取消");
                    throw new OperationCanceledException("场景加载被取消", cancellationToken);
                }

                await UniTask.Yield();
            }

            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid())
            {
                throw new JulyException($"[{Name}] 场景 {sceneName} 加载后无效");
            }

            return scene;
        }

        public async UniTask<bool> UnloadSceneAsync(string sceneName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                throw new ArgumentException("场景名称不能为空", nameof(sceneName));
            }

            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                LogWarning($"[{Name}] 场景 {sceneName} 未加载，无需卸载");
                return false;
            }

            var asyncOperation = SceneManager.UnloadSceneAsync(scene);
            if (asyncOperation == null)
            {
                LogWarning($"[{Name}] 场景 {sceneName} 卸载失败");
                return false;
            }

            while (!asyncOperation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    LogWarning($"[{Name}] 场景 {sceneName} 卸载被取消");
                    throw new OperationCanceledException("场景卸载被取消", cancellationToken);
                }

                await UniTask.Yield();
            }

            return true;
        }

        #endregion

        protected override UniTask OnShutdownAsync()
        {
            while (ResourceReleaseQueue.PendingCount > 0)
            {
                ResourceReleaseQueue.ProcessReleaseQueue(100);
            }

            UnloadAll();
            ResourceReleaseQueue.Clear();

            return UniTask.CompletedTask;
        }

        #region Private Methods

        private string NormalizePath(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            var path = System.IO.Path.ChangeExtension(input, null);

            const string resourcesPrefix = "Resources/";
            if (path.StartsWith(resourcesPrefix, StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(resourcesPrefix.Length);
            }

            path = path.Replace('\\', '/');
            return path;
        }

        private bool TryGetCachedResource<T>(string path, out T resource) where T : UnityEngine.Object
        {
            resource = null;
            if (_pathToResourceCache.TryGetValue(path, out var cachedObj))
            {
                if (cachedObj == null)
                {
                    _pathToResourceCache.TryRemove(path, out _);
                    return false;
                }

                if (cachedObj is T t)
                {
                    resource = t;
                    return true;
                }
            }

            return false;
        }

        private void IncrementRefCount(UnityEngine.Object obj)
        {
            _refCounts.AddOrUpdate(obj, 1, (key, oldValue) => oldValue + 1);
        }

        private bool DecrementRefCount(UnityEngine.Object obj)
        {
            if (!_refCounts.TryGetValue(obj, out var count))
            {
                return false;
            }

            if (count <= 1)
            {
                _refCounts.TryRemove(obj, out _);
                return true;
            }

            _refCounts[obj] = count - 1;
            return false;
        }

        private void RecordResourceMapping(string path, UnityEngine.Object resource)
        {
            _objectToPath[resource] = path;
            _pathToResourceCache[path] = resource;
        }

        private void CleanupResourceMappings(string path)
        {
            _pathToResourceCache.TryRemove(path, out _);

            var objectsToRemove = new List<UnityEngine.Object>();
            foreach (var kvp in _objectToPath)
            {
                if (kvp.Value == path)
                {
                    objectsToRemove.Add(kvp.Key);
                }
            }

            foreach (var obj in objectsToRemove)
            {
                _objectToPath.TryRemove(obj, out _);
            }
        }

        #endregion
    }
}
