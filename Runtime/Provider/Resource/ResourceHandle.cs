using System;
using UnityEngine;

namespace JulyCore.Provider.Resource
{
    /// <summary>
    /// 资源句柄
    /// 包装底层资源（如 YooAsset AssetHandle）的引用计数，释放完全委托给底层
    /// Dispose / BindTo / scope 三种方式触发释放，无 finalizer
    /// </summary>
    public sealed class ResourceHandle<T> : IDisposable where T : UnityEngine.Object
    {
        /// <summary>
        /// 资源对象
        /// </summary>
        public T Asset { get; private set; }

        /// <summary>
        /// 是否有效（资源存在且未释放）
        /// </summary>
        public bool IsValid => Asset != null && _release != null;

        /// <summary>
        /// 是否标记为永久持有（全局字体、常驻 Prefab 等），泄漏检测时跳过
        /// </summary>
        public bool IsPermanent { get; private set; }

        private Action _release;
        private ResourceHandleTracker _tracker;

        /// <summary>
        /// 构造句柄。release 委托用于释放底层资源引用（如 yooHandle.Release）
        /// </summary>
        public ResourceHandle(T asset, Action release)
        {
            Asset = asset;
            _release = release;
        }

        /// <summary>
        /// 标记为永久持有，泄漏检测会跳过此句柄。
        /// 调用后仍可正常 Dispose，仅影响诊断工具的判断。
        /// </summary>
        public ResourceHandle<T> MarkPermanent()
        {
            IsPermanent = true;
            return this;
        }

        /// <summary>
        /// 绑定到 GameObject，当 GameObject 销毁时自动释放资源
        /// </summary>
        public void BindTo(GameObject gameObject)
        {
            if (_release == null || gameObject == null) return;

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
            if (_release == null) return;

            var release = _release;
            _release = null;
            Asset = null;

            if (_tracker != null && _tracker.gameObject != null)
            {
                UnityEngine.Object.Destroy(_tracker);
            }
            _tracker = null;

            release();
        }
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
