using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Module.Resource;
using JulyCore.Provider.Resource;
using UnityEngine;

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
            /// 异步加载资源并返回句柄（引用计数由底层管理）。
            /// 
            /// 【使用示例】
            /// // 方式1：using 语句（一次性读取）
            /// using (var handle = await GF.Resource.LoadWithHandleAsync&lt;Sprite&gt;("icon"))
            /// {
            ///     image.sprite = handle.Asset;
            /// }
            /// 
            /// // 方式2：绑定到 GameObject（UI 场景推荐，GameObject 销毁时自动释放）
            /// var handle = await GF.Resource.LoadWithHandleAsync&lt;Sprite&gt;("icon");
            /// handle.BindTo(gameObject);
            /// image.sprite = handle.Asset;
            /// </summary>
            public static UniTask<ResourceHandle<T>> LoadWithHandleAsync<T>(string fileName,
                CancellationToken cancellationToken = default)
                where T : UnityEngine.Object
            {
                return Module.LoadWithHandleAsync<T>(fileName, cancellationToken);
            }

            /// <summary>
            /// 加载资源、在回调内取值后立即释放。
            /// 适用于配置、DLL 字节等"读取一次即丢弃"的场景，无需手动管理句柄。
            /// </summary>
            public static UniTask<TResult> LoadScopedAsync<T, TResult>(string fileName, Func<T, TResult> use,
                CancellationToken cancellationToken = default)
                where T : UnityEngine.Object
            {
                return Module.LoadScopedAsync(fileName, use, cancellationToken);
            }

            /// <summary>
            /// 加载资源并绑定到 GameObject 的生命周期，返回资源对象。
            /// GameObject 销毁时自动释放底层引用，上层无需管理句柄。
            /// 
            /// 【使用示例】
            /// var sprite = await GF.Resource.LoadAsync&lt;Sprite&gt;("icon", gameObject);
            /// image.sprite = sprite;
            /// </summary>
            public static UniTask<T> LoadAsync<T>(string fileName, GameObject bindTo,
                CancellationToken cancellationToken = default)
                where T : UnityEngine.Object
            {
                return Module.LoadAsync<T>(fileName, bindTo, cancellationToken);
            }

            /// <summary>
            /// 加载 Prefab 并实例化到指定父节点下，实例销毁时自动释放底层引用。
            /// 
            /// 【使用示例】
            /// var enemy = await GF.Resource.InstantiateAsync("EnemyPrefab", transform);
            /// </summary>
            public static UniTask<GameObject> InstantiateAsync(string fileName, Transform parent = null,
                CancellationToken cancellationToken = default)
            {
                return Module.InstantiateAsync(fileName, parent, cancellationToken);
            }

            /// <summary>
            /// 加载 Prefab、实例化并返回指定组件，实例销毁时自动释放底层引用。
            /// 
            /// 【使用示例】
            /// var hud = await GF.Resource.InstantiateAsync&lt;PlayerHUD&gt;("PlayerHUD", transform);
            /// </summary>
            public static UniTask<T> InstantiateAsync<T>(string fileName, Transform parent = null,
                CancellationToken cancellationToken = default)
                where T : Component
            {
                return Module.InstantiateAsync<T>(fileName, parent, cancellationToken);
            }

            /// <summary>
            /// 批量并行加载资源，返回句柄数组。
            /// 任一失败或取消时自动释放所有已完成句柄，保证不泄漏。
            /// </summary>
            public static UniTask<ResourceHandle<T>[]> LoadBatchAsync<T>(IReadOnlyList<string> fileNames,
                CancellationToken cancellationToken = default)
                where T : UnityEngine.Object
            {
                return Module.LoadBatchAsync<T>(fileNames, cancellationToken);
            }

            #endregion

            #region 下载

            /// <summary>
            /// 按 Tag 下载资源（含整体重试）
            /// </summary>
            public static UniTask<bool> DownloadByTagWithRetryAsync(string tag, int maxRetries = 3,
                CancellationToken ct = default)
            {
                return Module.DownloadByTagWithRetryAsync(tag, maxRetries, ct);
            }

            #endregion

            #region 检查

            /// <summary>
            /// 检查资源是否存在
            /// </summary>
            public static bool HasAsset(string fileName)
            {
                return Module.HasAsset(fileName);
            }

            #endregion
        }
    }
}
