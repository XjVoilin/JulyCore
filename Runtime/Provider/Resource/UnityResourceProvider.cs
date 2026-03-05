using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
    /// 核心设计：
    /// 1. 使用 Resources 文件夹：资源必须放在 Resources 文件夹中
    /// 2. 使用引用计数：每次 LoadAsync 增加引用计数，Unload 减少引用计数
    /// 3. 引用计数为 0 时释放资源
    /// 4. 资源对象缓存：同一 path 的资源对象会被复用
    /// 
    /// 注意：
    /// - Resources.Load 是同步的，但为了接口一致性，包装成异步方法
    /// - 生产环境建议使用 YooAssetResourceProvider 或其他 AssetBundle 方案
    /// </summary>
    public class UnityResourceProvider : ProviderBase, IResourceProvider
    {
        public override int Priority => Frameworkconst.PriorityResourceProvider;
        protected override LogChannel LogChannel => LogChannel.Resource;

        // 资源对象到引用计数的映射（业务层引用计数）
        private readonly ConcurrentDictionary<UnityEngine.Object, int> _refCounts = new();

        // Path到资源对象的映射（用于缓存和复用）
        private readonly ConcurrentDictionary<string, UnityEngine.Object> _pathToResourceCache = new();

        // 对象到Path的映射（用于通过对象卸载）
        private readonly ConcurrentDictionary<UnityEngine.Object, string> _objectToPath = new();

        // 预加载的资源
        private readonly ConcurrentDictionary<string, UnityEngine.Object> _preloadedResources = new();

        // 资源使用历史记录
        private readonly List<ResourceUsageHistory> _usageHistory = new List<ResourceUsageHistory>();
        private readonly object _usageHistoryLock = new object();
        private const int MaxUsageHistorySize = 1000;

        // 资源使用频率统计
        private readonly Dictionary<string, ResourceUsageFrequency> _usageFrequency = new Dictionary<string, ResourceUsageFrequency>();
        private readonly object _usageFrequencyLock = new object();

        // 活跃的资源句柄追踪（弱引用，不影响 GC）
        private readonly List<WeakReference<IResourceHandleInternal>> _activeHandles = new List<WeakReference<IResourceHandleInternal>>();
        private readonly object _activeHandlesLock = new object();

        /// <summary>
        /// 内部句柄接口（用于追踪）
        /// </summary>
        private interface IResourceHandleInternal
        {
            string Path { get; }
            string ResourceType { get; }
            DateTime LoadTime { get; }
            string LoadStackTrace { get; }
            bool IsDisposed { get; }
            bool IsBoundToObject { get; }
            string BoundObjectName { get; }
        }

        /// <summary>
        /// 可追踪的资源句柄（内部实现）
        /// </summary>
        private class TrackedResourceHandle<T> : ResourceHandle<T>, IResourceHandleInternal where T : UnityEngine.Object
        {
            public string ResourceType => typeof(T).Name;
            public bool IsBoundToObject { get; private set; }
            public string BoundObjectName { get; private set; }

            private readonly UnityResourceProvider _ownerProvider;

            internal TrackedResourceHandle(T asset, string path, UnityResourceProvider provider, bool captureStackTrace)
                : base(asset, path, provider, captureStackTrace)
            {
                _ownerProvider = provider;
            }

            public new void BindTo(GameObject gameObject)
            {
                base.BindTo(gameObject);
                IsBoundToObject = gameObject != null;
                BoundObjectName = gameObject?.name ?? string.Empty;
            }
        }

        /// <summary>
        /// 资源泄漏检测器
        /// </summary>
        private ResourceLeakDetector _leakDetector;

        /// <summary>
        /// Provider 初始化
        /// </summary>
        protected override UniTask OnInitAsync()
        {
            // 创建泄漏检测器
            _leakDetector = ResourceLeakDetector.CreateInstance(this);
            Log($"[{Name}] Unity Resources 资源提供者初始化完成，泄漏检测器已启用");
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 异步加载资源
        /// </summary>
        public async UniTask<T> LoadAsync<T>(string fileName, CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(fileName))
            {
                LogWarning($"[{Name}] 资源路径不能为空");
                return null;
            }

            var path = NormalizePath(fileName);

            // 检查缓存
            if (TryGetCachedResource<T>(path, out var cachedResource))
            {
                IncrementRefCount(cachedResource);
                RecordUsageHistory(path, "Load", _refCounts.GetValueOrDefault(cachedResource, 0));
                return cachedResource;
            }

            // 检查预加载
            if (_preloadedResources.TryRemove(path, out var preloadedObj) && preloadedObj is T preloaded)
            {
                RecordResourceMapping(path, preloaded);
                IncrementRefCount(preloaded);
                RecordUsageHistory(path, "Load", _refCounts.GetValueOrDefault(preloaded, 0));
                Log($"[{Name}] 从预加载获取资源: {path}");
                return preloaded;
            }

            try
            {
                // 使用 Resources.LoadAsync 进行异步加载
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

                // 记录资源映射并增加引用计数
                RecordResourceMapping(path, resource);
                IncrementRefCount(resource);
                RecordUsageHistory(path, "Load", _refCounts.GetValueOrDefault(resource, 0));

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

        /// <summary>
        /// 异步加载资源并返回句柄（自动管理引用计数）
        /// </summary>
        public async UniTask<ResourceHandle<T>> LoadWithHandleAsync<T>(string fileName, bool captureStackTrace = false, CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            var asset = await LoadAsync<T>(fileName, cancellationToken);
            if (asset == null)
            {
                return null;
            }

            var path = NormalizePath(fileName);
            var handle = new TrackedResourceHandle<T>(asset, path, this, captureStackTrace);

            // 追踪句柄
            lock (_activeHandlesLock)
            {
                // 清理已释放的句柄
                CleanupReleasedHandles();
                _activeHandles.Add(new WeakReference<IResourceHandleInternal>(handle));
            }

            return handle;
        }

        /// <summary>
        /// 清理已释放的句柄引用
        /// </summary>
        private void CleanupReleasedHandles()
        {
            _activeHandles.RemoveAll(weakRef =>
            {
                if (!weakRef.TryGetTarget(out var handle))
                {
                    return true; // 已被 GC 回收
                }
                return handle.IsDisposed;
            });
        }

        /// <summary>
        /// 预加载资源
        /// </summary>
        public async UniTask<bool> PreloadAsync<T>(string fileName, CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            var path = NormalizePath(fileName);

            // 检查是否已预加载或已加载
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

        /// <summary>
        /// 批量加载资源
        /// </summary>
        public async UniTask<List<T>> LoadBatchAsync<T>(IEnumerable<string> fileNames, CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            var results = new List<T>();
            foreach (var fileName in fileNames)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var resource = await LoadAsync<T>(fileName, cancellationToken);
                results.Add(resource);
            }

            return results;
        }

        /// <summary>
        /// 加载子资源
        /// </summary>
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
                // Resources.LoadAll 加载所有子资源
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

        /// <summary>
        /// 批量加载子资源
        /// </summary>
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

        /// <summary>
        /// 检查资源是否存在
        /// </summary>
        public bool HasAsset(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            var path = NormalizePath(fileName);

            // 检查缓存
            if (_pathToResourceCache.ContainsKey(path) || _preloadedResources.ContainsKey(path))
            {
                return true;
            }

            // 尝试加载检查
            var resource = Resources.Load(path);
            if (resource != null)
            {
                // 不缓存，只是检查
                Resources.UnloadAsset(resource);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 卸载资源
        /// </summary>
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
                    // 引用计数为0，释放资源
                    CleanupResourceMappings(path);

                    // Resources.UnloadAsset 只能卸载非 GameObject 资源
                    if (!(obj is GameObject))
                    {
                        Resources.UnloadAsset(obj);
                    }

                    Log($"[{Name}] 资源已卸载: {path}");
                    RecordUsageHistory(path, "Unload", 0);
                }
                else
                {
                    RecordUsageHistory(path, "Unload", _refCounts.GetValueOrDefault(obj, 0));
                }
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 卸载资源异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 卸载所有资源
        /// </summary>
        public void UnloadAll()
        {
            try
            {
                // 清理所有映射
                _pathToResourceCache.Clear();
                _objectToPath.Clear();
                _refCounts.Clear();
                _preloadedResources.Clear();

                // 卸载未使用的资源
                Resources.UnloadUnusedAssets();
                GC.Collect();

                Log($"[{Name}] 所有资源已卸载");
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 卸载所有资源异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取资源的引用计数
        /// </summary>
        public int GetRefCount(UnityEngine.Object obj)
        {
            return obj == null ? 0 : _refCounts.GetValueOrDefault(obj, 0);
        }

        /// <summary>
        /// 获取资源使用统计信息
        /// </summary>
        public ResourceStatistics GetStatistics()
        {
            int activeHandleCount;
            lock (_activeHandlesLock)
            {
                CleanupReleasedHandles();
                activeHandleCount = _activeHandles.Count;
            }

            return new ResourceStatistics
            {
                TotalResources = _refCounts.Count,
                TotalRefCount = _refCounts.Values.Sum(),
                ActiveHandles = activeHandleCount,
                PreloadedResources = _preloadedResources.Count,
                CachedResources = _pathToResourceCache.Count
            };
        }

        /// <summary>
        /// 检测资源泄漏
        /// </summary>
        public List<ResourceLeakInfo> DetectLeaks(int leakThreshold = 10)
        {
            var leaks = new List<ResourceLeakInfo>();

            foreach (var kvp in _refCounts)
            {
                var obj = kvp.Key;
                var refCount = kvp.Value;

                if (refCount > leakThreshold && obj != null)
                {
                    string path = "Unknown";
                    if (_objectToPath.TryGetValue(obj, out var p))
                    {
                        path = p;
                    }

                    leaks.Add(new ResourceLeakInfo
                    {
                        Location = path,
                        ResourceName = obj.name,
                        RefCount = refCount,
                        ResourceType = obj.GetType().Name
                    });
                }
            }

            return leaks;
        }

        /// <summary>
        /// 获取所有已加载资源的详细信息
        /// </summary>
        public List<ResourceInfo> GetAllResources()
        {
            var resources = new List<ResourceInfo>();

            foreach (var kvp in _refCounts)
            {
                var obj = kvp.Key;
                var refCount = kvp.Value;

                if (obj == null) continue;

                string path = "Unknown";
                if (_objectToPath.TryGetValue(obj, out var p))
                {
                    path = p;
                }

                bool isCached = _pathToResourceCache.ContainsKey(path);

                resources.Add(new ResourceInfo
                {
                    Location = path,
                    ResourceName = obj.name,
                    RefCount = refCount,
                    ResourceType = obj.GetType().Name,
                    IsCached = isCached,
                    HandleCount = 0
                });
            }

            return resources;
        }

        /// <summary>
        /// 获取资源使用历史
        /// </summary>
        public List<ResourceUsageHistory> GetResourceUsageHistory(string location = null, int maxCount = 100)
        {
            lock (_usageHistoryLock)
            {
                var query = _usageHistory.AsEnumerable();

                if (!string.IsNullOrEmpty(location))
                {
                    query = query.Where(h => h.Location == location);
                }

                return query
                    .OrderByDescending(h => h.Timestamp)
                    .Take(maxCount)
                    .ToList();
            }
        }

        /// <summary>
        /// 获取资源使用频率统计
        /// </summary>
        public List<ResourceUsageFrequency> GetResourceUsageFrequency(int topN = 20)
        {
            lock (_usageFrequencyLock)
            {
                return _usageFrequency.Values
                    .OrderByDescending(f => f.TotalUsageCount)
                    .ThenByDescending(f => f.LastUsedTime)
                    .Take(topN)
                    .ToList();
            }
        }

        /// <summary>
        /// 获取活跃的资源句柄信息
        /// </summary>
        public List<ActiveHandleInfo> GetActiveHandles()
        {
            var result = new List<ActiveHandleInfo>();
            var now = DateTime.Now;

            lock (_activeHandlesLock)
            {
                CleanupReleasedHandles();

                foreach (var weakRef in _activeHandles)
                {
                    if (weakRef.TryGetTarget(out var handle) && !handle.IsDisposed)
                    {
                        result.Add(new ActiveHandleInfo
                        {
                            Path = handle.Path,
                            ResourceType = handle.ResourceType,
                            LoadTime = handle.LoadTime,
                            AliveSeconds = (now - handle.LoadTime).TotalSeconds,
                            LoadStackTrace = handle.LoadStackTrace,
                            IsBoundToObject = handle.IsBoundToObject,
                            BoundObjectName = handle.BoundObjectName
                        });
                    }
                }
            }

            return result.OrderByDescending(h => h.AliveSeconds).ToList();
        }

        #region 场景加载

        /// <summary>
        /// 异步加载场景
        /// </summary>
        public async UniTask<UnityEngine.SceneManagement.Scene> LoadSceneAsync(
            string sceneName,
            LoadSceneMode loadSceneMode = LoadSceneMode.Single,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                throw new ArgumentException("场景名称不能为空", nameof(sceneName));
            }

            // 检查场景是否已加载
            var existingScene = SceneManager.GetSceneByName(sceneName);
            if (existingScene.IsValid() && existingScene.isLoaded)
            {
                LogWarning($"[{Name}] 场景 {sceneName} 已加载，直接返回");
                return existingScene;
            }

            // 使用 Unity SceneManager 加载场景
            var asyncOperation = SceneManager.LoadSceneAsync(sceneName, loadSceneMode);
            if (asyncOperation == null)
            {
                throw new JulyException($"[{Name}] 场景 {sceneName} 加载失败（场景不存在或路径错误）");
            }

            asyncOperation.allowSceneActivation = true;

            // 等待加载完成
            while (!asyncOperation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    LogWarning($"[{Name}] 场景 {sceneName} 加载被取消");
                    throw new OperationCanceledException("场景加载被取消", cancellationToken);
                }

                await UniTask.Yield();
            }

            // 获取加载后的场景
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid())
            {
                throw new JulyException($"[{Name}] 场景 {sceneName} 加载后无效");
            }

            Log($"[{Name}] 场景 {sceneName} 加载完成");
            return scene;
        }

        /// <summary>
        /// 异步卸载场景
        /// </summary>
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

            // 等待卸载完成
            while (!asyncOperation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    LogWarning($"[{Name}] 场景 {sceneName} 卸载被取消");
                    throw new OperationCanceledException("场景卸载被取消", cancellationToken);
                }

                await UniTask.Yield();
            }

            Log($"[{Name}] 场景 {sceneName} 卸载完成");
            return true;
        }

        #endregion

        /// <summary>
        /// Provider 关闭
        /// </summary>
        protected override UniTask OnShutdownAsync()
        {
            // 先处理释放队列中的资源
            while (ResourceReleaseQueue.PendingCount > 0)
            {
                ResourceReleaseQueue.ProcessReleaseQueue(100);
            }

            // 清空句柄追踪
            lock (_activeHandlesLock)
            {
                _activeHandles.Clear();
            }

            // 卸载所有资源
            UnloadAll();

            // 清理释放队列
            ResourceReleaseQueue.Clear();

            Log($"[{Name}] Unity Resources 资源提供者已关闭");
            return UniTask.CompletedTask;
        }

        #region Private Methods

        /// <summary>
        /// 规范化路径（移除扩展名和 Resources/ 前缀）
        /// </summary>
        private string NormalizePath(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            // 移除扩展名
            var path = System.IO.Path.ChangeExtension(input, null);

            // 移除 Resources/ 前缀（如果有）
            const string resourcesPrefix = "Resources/";
            if (path.StartsWith(resourcesPrefix, StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(resourcesPrefix.Length);
            }

            // 统一使用正斜杠
            path = path.Replace('\\', '/');

            return path;
        }

        /// <summary>
        /// 尝试从缓存获取资源
        /// </summary>
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

        /// <summary>
        /// 增加资源引用计数
        /// </summary>
        private void IncrementRefCount(UnityEngine.Object obj)
        {
            _refCounts.AddOrUpdate(obj, 1, (key, oldValue) => oldValue + 1);
        }

        /// <summary>
        /// 减少资源引用计数
        /// </summary>
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

        /// <summary>
        /// 记录资源映射关系
        /// </summary>
        private void RecordResourceMapping(string path, UnityEngine.Object resource)
        {
            _objectToPath[resource] = path;
            _pathToResourceCache[path] = resource;
        }

        /// <summary>
        /// 清理资源映射关系
        /// </summary>
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

        /// <summary>
        /// 记录资源使用历史
        /// </summary>
        private void RecordUsageHistory(string path, string operation, int refCount)
        {
            lock (_usageHistoryLock)
            {
                _usageHistory.Add(new ResourceUsageHistory
                {
                    Location = path,
                    Operation = operation,
                    Timestamp = DateTime.Now,
                    RefCount = refCount
                });

                if (_usageHistory.Count > MaxUsageHistorySize)
                {
                    _usageHistory.RemoveAt(0);
                }
            }

            lock (_usageFrequencyLock)
            {
                if (!_usageFrequency.TryGetValue(path, out var frequency))
                {
                    frequency = new ResourceUsageFrequency { Location = path };
                    _usageFrequency[path] = frequency;
                }

                if (operation == "Load")
                {
                    frequency.LoadCount++;
                }
                else if (operation == "Unload")
                {
                    frequency.UnloadCount++;
                }

                frequency.LastUsedTime = DateTime.Now;
            }
        }

        #endregion
    }
}

