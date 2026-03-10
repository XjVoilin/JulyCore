using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using UnityEngine;

namespace JulyCore.Provider.Resource
{
    /// <summary>
    /// 资源释放队列管理器（主线程安全释放）
    /// 用于处理析构函数中无法直接调用 Unity API 的问题
    /// </summary>
    public static class ResourceReleaseQueue
    {
        private static readonly ConcurrentQueue<PendingRelease> _pendingReleases = new ConcurrentQueue<PendingRelease>();

        private struct PendingRelease
        {
            public UnityEngine.Object Asset;
            public IResourceProvider Provider;
            public string Path;
        }

        /// <summary>
        /// 将资源加入释放队列（可在任何线程调用）
        /// </summary>
        public static void EnqueueRelease(UnityEngine.Object asset, IResourceProvider provider, string path)
        {
            if (asset == null || provider == null) return;
            _pendingReleases.Enqueue(new PendingRelease { Asset = asset, Provider = provider, Path = path });
        }

        /// <summary>
        /// 处理释放队列（必须在主线程调用）
        /// </summary>
        /// <param name="maxCount">单次最大处理数量（防止卡顿）</param>
        /// <returns>本次处理的数量</returns>
        public static int ProcessReleaseQueue(int maxCount = 10)
        {
            int processed = 0;
            while (processed < maxCount && _pendingReleases.TryDequeue(out var pending))
            {
                try
                {
                    if (pending.Asset != null && pending.Provider != null)
                    {
                        pending.Provider.Unload(pending.Asset);
                    }
                }
                catch (Exception ex)
                {
                    Core.JLogger.LogError($"[ResourceReleaseQueue] 释放资源失败: {pending.Path}, 错误: {ex.Message}");
                }
                processed++;
            }
            return processed;
        }

        public static int PendingCount => _pendingReleases.Count;

        public static void Clear()
        {
            while (_pendingReleases.TryDequeue(out _)) { }
        }
    }

    /// <summary>
    /// 资源句柄基类（提供公共实现）
    /// </summary>
    public abstract class ResourceHandleBase : IDisposable
    {
        /// <summary>
        /// 资源路径
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// 是否已释放
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// 加载时间
        /// </summary>
        public DateTime LoadTime { get; }

        /// <summary>
        /// 加载调用栈（用于定位泄漏）
        /// </summary>
        public string LoadStackTrace { get; }

        /// <summary>
        /// 资源提供者引用
        /// </summary>
        protected readonly IResourceProvider _provider;

        private ResourceHandleTracker _tracker;

        protected ResourceHandleBase(string path, IResourceProvider provider, bool captureStackTrace)
        {
            Path = path;
            _provider = provider;
            LoadTime = DateTime.Now;
            IsDisposed = false;

            if (captureStackTrace)
            {
                LoadStackTrace = new StackTrace(3, true).ToString();
            }
        }

        /// <summary>
        /// 获取资源对象（由子类实现）
        /// </summary>
        protected abstract UnityEngine.Object GetAssetObject();

        /// <summary>
        /// 清除资源引用（由子类实现）
        /// </summary>
        protected abstract void ClearAsset();

        /// <summary>
        /// 是否有效（资源存在且未释放）
        /// </summary>
        public bool IsValid => !IsDisposed && GetAssetObject() != null;

        /// <summary>
        /// 绑定到 GameObject，当 GameObject 销毁时自动释放资源
        /// </summary>
        public void BindTo(GameObject gameObject)
        {
            if (IsDisposed || gameObject == null) return;

            // 如果已绑定到其他对象，先解绑
            if (_tracker != null)
            {
                UnityEngine.Object.Destroy(_tracker);
                _tracker = null;
            }

            _tracker = gameObject.AddComponent<ResourceHandleTracker>();
            _tracker.Initialize(this);
        }

        /// <summary>
        /// 绑定到 Component
        /// </summary>
        public void BindTo(UnityEngine.Component component)
        {
            if (component != null)
            {
                BindTo(component.gameObject);
            }
        }

        /// <summary>
        /// 释放资源引用
        /// </summary>
        public void Dispose()
        {
            if (IsDisposed) return;

            IsDisposed = true;

            // 清理绑定
            if (_tracker != null && _tracker.gameObject != null)
            {
                UnityEngine.Object.Destroy(_tracker);
            }
            _tracker = null;

            // 释放资源引用
            var asset = GetAssetObject();
            if (asset != null && _provider != null)
            {
                _provider.Unload(asset);
            }

            ClearAsset();
        }

        /// <summary>
        /// 析构函数（防止忘记释放）
        /// </summary>
        ~ResourceHandleBase()
        {
            if (!IsDisposed)
            {
                var asset = GetAssetObject();
                if (asset != null)
                {
                    Core.JLogger.LogWarning(
                        $"[ResourceHandle] 资源句柄未释放! Path={Path}, LoadTime={LoadTime:HH:mm:ss}\n" +
                        $"加载位置:\n{LoadStackTrace ?? "未捕获调用栈（可通过 captureStackTrace 参数启用）"}");

                    ResourceReleaseQueue.EnqueueRelease(asset, _provider, Path);
                    IsDisposed = true;
                    ClearAsset();
                }
            }
        }
    }

    /// <summary>
    /// 泛型资源句柄
    /// </summary>
    public class ResourceHandle<T> : ResourceHandleBase where T : UnityEngine.Object
    {
        public T Asset { get; private set; }

        public ResourceHandle(T asset, string path, IResourceProvider provider, bool captureStackTrace = false)
            : base(path, provider, captureStackTrace)
        {
            Asset = asset;
        }

        protected override UnityEngine.Object GetAssetObject() => Asset;
        protected override void ClearAsset() => Asset = null;

        public static implicit operator T(ResourceHandle<T> handle) => handle?.Asset;
    }

    /// <summary>
    /// 非泛型资源句柄
    /// </summary>
    public class ResourceHandle : ResourceHandleBase
    {
        public UnityEngine.Object Asset { get; private set; }

        internal ResourceHandle(UnityEngine.Object asset, string path, IResourceProvider provider, bool captureStackTrace = false)
            : base(path, provider, captureStackTrace)
        {
            Asset = asset;
        }

        protected override UnityEngine.Object GetAssetObject() => Asset;
        protected override void ClearAsset() => Asset = null;

        public TAsset GetAsset<TAsset>() where TAsset : UnityEngine.Object => Asset as TAsset;
    }

    /// <summary>
    /// 资源句柄追踪器（用于绑定 GameObject 生命周期）
    /// </summary>
    internal class ResourceHandleTracker : MonoBehaviour
    {
        private IDisposable _handle;
        private static bool _isApplicationQuitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _isApplicationQuitting = false;
        }

        internal void Initialize(IDisposable handle)
        {
            _handle = handle;
        }

        private void OnApplicationQuit()
        {
            _isApplicationQuitting = true;
        }

        private void OnDestroy()
        {
            if (!_isApplicationQuitting)
            {
                _handle?.Dispose();
            }
            _handle = null;
        }
    }
}
