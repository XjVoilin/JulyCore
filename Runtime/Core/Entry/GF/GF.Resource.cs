using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
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

            #region 传统 API（返回值 + 异常）

            /// <summary>
            /// 异步加载资源
            /// </summary>
            /// <typeparam name="T">资源类型</typeparam>
            /// <param name="fileName">资源文件名（不含扩展名）</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>资源对象，如果加载失败则返回null</returns>
            public static UniTask<T> LoadAsync<T>(string fileName, CancellationToken cancellationToken = default) where T : UnityEngine.Object
            {
                return Module.LoadAsync<T>(fileName, cancellationToken);
            }

            #endregion

            #region Result 模式 API（推荐）

            /// <summary>
            /// 异步加载资源（Result 模式）
            /// 
            /// 【优势】
            /// - 不抛出异常，通过 Result 返回错误信息
            /// - 可以获取详细的错误码和错误消息
            /// - 支持链式操作和错误恢复
            /// 
            /// 【使用示例】
            /// var result = await GF.Resource.TryLoadAsync&lt;Sprite&gt;("icon_player");
            /// if (result.IsSuccess)
            /// {
            ///     var sprite = result.Value;
            /// }
            /// else
            /// {
            ///     JLogger.LogWarning($"加载失败: {result.Message}");
            ///     // 使用默认图片
            /// }
            /// </summary>
            public static UniTask<FrameworkResult<T>> TryLoadAsync<T>(string fileName, CancellationToken cancellationToken = default) where T : UnityEngine.Object
            {
                return Module.TryLoadAsync<T>(fileName, cancellationToken);
            }

            #endregion

            #region Handle 模式 API（自动引用计数，强烈推荐）

            /// <summary>
            /// 异步加载资源并返回句柄（自动管理引用计数）
            /// 
            /// 【核心优势】
            /// - 自动引用计数：句柄释放时自动 Unload，无需手动管理
            /// - 泄漏定位：可捕获加载调用栈，精确定位泄漏来源
            /// - 生命周期绑定：可绑定到 GameObject，随对象销毁自动释放
            /// 
            /// 【使用示例】
            /// // 方式1：using 语句（最安全）
            /// using (var handle = await GF.Resource.LoadWithHandleAsync&lt;Sprite&gt;("icon"))
            /// {
            ///     image.sprite = handle.Asset;
            /// } // 自动释放
            /// 
            /// // 方式2：绑定到 GameObject（UI 场景推荐）
            /// var handle = await GF.Resource.LoadWithHandleAsync&lt;Sprite&gt;("icon");
            /// handle.BindTo(gameObject); // GameObject 销毁时自动释放
            /// image.sprite = handle.Asset;
            /// 
            /// // 方式3：手动管理（需要长期持有时）
            /// _iconHandle = await GF.Resource.LoadWithHandleAsync&lt;Sprite&gt;("icon");
            /// // ... 使用资源 ...
            /// _iconHandle.Dispose(); // 手动释放
            /// </summary>
            /// <typeparam name="T">资源类型</typeparam>
            /// <param name="fileName">资源文件名</param>
            /// <param name="captureStackTrace">是否捕获调用栈（用于泄漏定位，Debug 时开启）</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>资源句柄</returns>
            public static UniTask<ResourceHandle<T>> LoadWithHandleAsync<T>(string fileName, bool captureStackTrace = false, CancellationToken cancellationToken = default) where T : UnityEngine.Object
            {
                return Module.LoadWithHandleAsync<T>(fileName, captureStackTrace, cancellationToken);
            }

            /// <summary>
            /// 获取活跃的资源句柄信息（用于调试和泄漏检测）
            /// </summary>
            /// <returns>活跃句柄列表，按存活时长排序</returns>
            public static List<ActiveHandleInfo> GetActiveHandles()
            {
                return Module.GetActiveHandles();
            }

            /// <summary>
            /// 打印资源泄漏报告（开发调试用）
            /// </summary>
            /// <param name="aliveSecondsThreshold">存活时长阈值（秒），超过此时长的句柄视为可疑</param>
            public static void PrintLeakReport(double aliveSecondsThreshold = 60)
            {
                Module.PrintLeakReport(aliveSecondsThreshold);
            }

            #endregion

            #region 批量加载

            /// <summary>
            /// 预加载资源（不增加引用计数，仅将资源加载到内存）
            /// </summary>
            /// <typeparam name="T">资源类型</typeparam>
            /// <param name="fileName">资源文件名（不含扩展名）</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>是否预加载成功</returns>
            public static UniTask<bool> PreloadAsync<T>(string fileName, CancellationToken cancellationToken = default) where T : UnityEngine.Object
            {
                return Module.PreloadAsync<T>(fileName, cancellationToken);
            }

            /// <summary>
            /// 批量加载资源
            /// </summary>
            /// <typeparam name="T">资源类型</typeparam>
            /// <param name="fileNames">资源文件名列表（不含扩展名）</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>资源对象列表，加载失败的资源为null</returns>
            public static UniTask<List<T>> LoadBatchAsync<T>(IEnumerable<string> fileNames, CancellationToken cancellationToken = default) where T : UnityEngine.Object
            {
                return Module.LoadBatchAsync<T>(fileNames, cancellationToken);
            }

            #endregion

            #region 子资源加载

            /// <summary>
            /// 加载子资源（如SpriteAtlas中的Sprite、AudioClip等）
            /// </summary>
            /// <typeparam name="T">子资源类型</typeparam>
            /// <param name="fileName">资源文件名（不含扩展名）</param>
            /// <param name="assetName">子资源名称</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>子资源对象，加载失败返回null</returns>
            public static UniTask<T> LoadSubAssetAsync<T>(string fileName, string assetName, CancellationToken cancellationToken = default) where T : UnityEngine.Object
            {
                return Module.LoadSubAssetAsync<T>(fileName, assetName, cancellationToken);
            }

            /// <summary>
            /// 批量加载子资源
            /// </summary>
            /// <typeparam name="T">子资源类型</typeparam>
            /// <param name="fileName">资源文件名（不含扩展名）</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>所有子资源列表</returns>
            public static UniTask<List<T>> LoadAllSubAssetsAsync<T>(string fileName, CancellationToken cancellationToken = default) where T : UnityEngine.Object
            {
                return Module.LoadAllSubAssetsAsync<T>(fileName, cancellationToken);
            }

            #endregion

            #region 资源检查与卸载

            /// <summary>
            /// 检查资源是否存在
            /// </summary>
            /// <param name="fileName">资源文件名（不含扩展名）</param>
            /// <returns>是否存在</returns>
            public static bool HasAsset(string fileName)
            {
                return Module.HasAsset(fileName);
            }

            /// <summary>
            /// 卸载资源（手动引用计数管理，建议使用 Handle 模式代替）
            /// </summary>
            /// <param name="obj">要卸载的资源对象</param>
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

            /// <summary>
            /// 获取资源的引用计数
            /// </summary>
            /// <param name="obj">资源对象</param>
            /// <returns>引用计数，如果资源不存在返回0</returns>
            public static int GetRefCount(UnityEngine.Object obj)
            {
                return Module.GetRefCount(obj);
            }

            #endregion

            #region 统计与诊断

            /// <summary>
            /// 获取资源使用统计信息
            /// </summary>
            public static ResourceStatistics GetStatistics()
            {
                return Module.GetStatistics();
            }

            /// <summary>
            /// 检测资源泄漏
            /// </summary>
            /// <param name="leakThreshold">泄漏阈值（引用计数超过此值视为可能泄漏，默认10）</param>
            public static List<ResourceLeakInfo> DetectLeaks(int leakThreshold = 10)
            {
                return Module.DetectLeaks(leakThreshold);
            }

            /// <summary>
            /// 获取所有已加载资源的详细信息
            /// </summary>
            public static List<ResourceInfo> GetAllResources()
            {
                return Module.GetAllResources();
            }

            /// <summary>
            /// 获取资源使用历史
            /// </summary>
            /// <param name="location">资源位置（可选，为空则返回所有资源的历史）</param>
            /// <param name="maxCount">最大返回数量，默认100</param>
            public static List<ResourceUsageHistory> GetResourceUsageHistory(string location = null, int maxCount = 100)
            {
                return Module.GetResourceUsageHistory(location, maxCount);
            }

            /// <summary>
            /// 获取资源使用频率统计
            /// </summary>
            /// <param name="topN">返回前N个最常使用的资源，默认20</param>
            public static List<ResourceUsageFrequency> GetResourceUsageFrequency(int topN = 20)
            {
                return Module.GetResourceUsageFrequency(topN);
            }

            #endregion
        }
    }
}
