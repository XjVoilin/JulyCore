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
    /// 【职责】
    /// - 业务语义层：资源预加载策略、资源分组管理、加载优先级调度
    /// - 状态管理：预加载队列、资源分组状态
    /// - 事件通知：资源加载完成、内存警告等事件
    /// 
    /// 【当前状态】
    /// 技术代理层 - 当前主要转发 Provider 调用
    /// 未来可扩展：预加载队列、资源分组、内存管理策略等业务规则
    /// 
    /// 【通信模式】
    /// - 调用 Provider：执行资源加载/卸载的技术操作
    /// - 发布 Event：可通知外部资源状态变化
    /// </summary>
    internal class ResourceModule : ModuleBase
    {
        private IResourceProvider _resourceProvider;

        /// <summary>
        /// 日志通道
        /// </summary>
        protected override LogChannel LogChannel => LogChannel.Resource;

        /// <summary>
        /// 模块执行优先级
        /// </summary>
        public override int Priority => Frameworkconst.PriorityResourceModule;

        /// <summary>
        /// 初始化Module
        /// </summary>
        protected override UniTask OnInitAsync()
        {
            try
            {
                _resourceProvider = GetProvider<IResourceProvider>();
                Log($"[{Name}] 资源模块初始化完成");
                return base.OnInitAsync();
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 资源模块初始化失败: {ex.Message}");
                throw;
            }
        }

        #region 资源加载（传统API）

        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="fileName">资源文件名（不含扩展名）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>资源对象，如果加载失败则返回null</returns>
        internal UniTask<T> LoadAsync<T>(string fileName, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            EnsureProvider();
            return _resourceProvider.LoadAsync<T>(fileName, cancellationToken);
        }

        #endregion

        #region Result 模式 API

        /// <summary>
        /// 异步加载资源（Result 模式）
        /// </summary>
        internal async UniTask<FrameworkResult<T>> TryLoadAsync<T>(string fileName, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            if (_resourceProvider == null)
            {
                return FrameworkResult<T>.Failure(FrameworkErrorCode.ProviderNotFound, "IResourceProvider未注册");
            }

            if (string.IsNullOrEmpty(fileName))
            {
                return FrameworkResult<T>.Failure(FrameworkErrorCode.InvalidArgument, "资源名称不能为空");
            }

            try
            {
                var resource = await _resourceProvider.LoadAsync<T>(fileName, cancellationToken);
                if (resource == null)
                {
                    return FrameworkResult<T>.Failure(FrameworkErrorCode.ResourceNotFound, $"资源未找到: {fileName}");
                }
                return FrameworkResult<T>.Success(resource);
            }
            catch (OperationCanceledException)
            {
                return FrameworkResult<T>.Failure(FrameworkErrorCode.Cancelled, "资源加载被取消");
            }
            catch (Exception ex)
            {
                return FrameworkResult<T>.Failure(FrameworkErrorCode.ResourceLoadFailed, $"资源加载失败: {ex.Message}", ex);
            }
        }

        #endregion

        #region Handle 模式 API（推荐）

        /// <summary>
        /// 异步加载资源并返回句柄（自动管理引用计数）
        /// </summary>
        internal UniTask<ResourceHandle<T>> LoadWithHandleAsync<T>(string fileName, bool captureStackTrace = false, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            EnsureProvider();
            return _resourceProvider.LoadWithHandleAsync<T>(fileName, captureStackTrace, cancellationToken);
        }

        /// <summary>
        /// 获取活跃的资源句柄信息
        /// </summary>
        internal List<ActiveHandleInfo> GetActiveHandles()
        {
            return _resourceProvider?.GetActiveHandles() ?? new List<ActiveHandleInfo>();
        }

        #endregion

        #region 批量加载

        /// <summary>
        /// 预加载资源
        /// </summary>
        internal UniTask<bool> PreloadAsync<T>(string fileName, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            EnsureProvider();
            return _resourceProvider.PreloadAsync<T>(fileName, cancellationToken);
        }

        /// <summary>
        /// 批量加载资源
        /// </summary>
        internal UniTask<List<T>> LoadBatchAsync<T>(IEnumerable<string> fileNames, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            EnsureProvider();
            return _resourceProvider.LoadBatchAsync<T>(fileNames, cancellationToken);
        }

        #endregion

        #region 子资源加载

        /// <summary>
        /// 加载子资源
        /// </summary>
        internal UniTask<T> LoadSubAssetAsync<T>(string fileName, string assetName, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            EnsureProvider();
            return _resourceProvider.LoadSubAssetAsync<T>(fileName, assetName, cancellationToken);
        }

        /// <summary>
        /// 批量加载子资源
        /// </summary>
        internal UniTask<List<T>> LoadAllSubAssetsAsync<T>(string fileName, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            EnsureProvider();
            return _resourceProvider.LoadAllSubAssetsAsync<T>(fileName, cancellationToken);
        }

        #endregion

        #region 资源检查与卸载

        /// <summary>
        /// 检查资源是否存在
        /// </summary>
        internal bool HasAsset(string fileName)
        {
            return _resourceProvider?.HasAsset(fileName) ?? false;
        }

        /// <summary>
        /// 卸载资源
        /// </summary>
        internal void Unload(UnityEngine.Object obj)
        {
            if (obj == null) return;
            _resourceProvider?.Unload(obj);
        }

        /// <summary>
        /// 卸载所有资源
        /// </summary>
        internal void UnloadAll()
        {
            _resourceProvider?.UnloadAll();
        }

        /// <summary>
        /// 获取资源的引用计数
        /// </summary>
        internal int GetRefCount(UnityEngine.Object obj)
        {
            return _resourceProvider?.GetRefCount(obj) ?? 0;
        }

        #endregion

        #region 统计与诊断

        /// <summary>
        /// 获取资源使用统计信息
        /// </summary>
        internal ResourceStatistics GetStatistics()
        {
            return _resourceProvider?.GetStatistics() ?? new ResourceStatistics();
        }

        /// <summary>
        /// 检测资源泄漏
        /// </summary>
        internal List<ResourceLeakInfo> DetectLeaks(int leakThreshold = 10)
        {
            return _resourceProvider?.DetectLeaks(leakThreshold) ?? new List<ResourceLeakInfo>();
        }

        /// <summary>
        /// 获取所有已加载资源的详细信息
        /// </summary>
        internal List<ResourceInfo> GetAllResources()
        {
            return _resourceProvider?.GetAllResources() ?? new List<ResourceInfo>();
        }

        /// <summary>
        /// 获取资源使用历史
        /// </summary>
        internal List<ResourceUsageHistory> GetResourceUsageHistory(string location = null, int maxCount = 100)
        {
            return _resourceProvider?.GetResourceUsageHistory(location, maxCount) ?? new List<ResourceUsageHistory>();
        }

        /// <summary>
        /// 获取资源使用频率统计
        /// </summary>
        internal List<ResourceUsageFrequency> GetResourceUsageFrequency(int topN = 20)
        {
            return _resourceProvider?.GetResourceUsageFrequency(topN) ?? new List<ResourceUsageFrequency>();
        }

        /// <summary>
        /// 打印资源泄漏报告
        /// </summary>
        internal void PrintLeakReport(double aliveSecondsThreshold = 60)
        {
            var handles = GetActiveHandles();
            var suspicious = handles.FindAll(h => h.AliveSeconds > aliveSecondsThreshold);

            if (suspicious.Count == 0)
            {
                Log($"[{Name}] 未发现可疑的资源泄漏（阈值: {aliveSecondsThreshold}秒）");
                return;
            }

            LogWarning($"[{Name}] 发现 {suspicious.Count} 个可疑的资源句柄（存活超过 {aliveSecondsThreshold}秒）:");
            foreach (var handle in suspicious)
            {
                var bindInfo = handle.IsBoundToObject ? $", 绑定到: {handle.BoundObjectName}" : "";
                LogWarning(
                    $"  - {handle.Path} ({handle.ResourceType})\n" +
                    $"    存活: {handle.AliveSeconds:F1}秒, 加载时间: {handle.LoadTime:HH:mm:ss}{bindInfo}\n" +
                    $"    调用栈: {handle.LoadStackTrace ?? "未捕获"}");
            }
        }

        #endregion

        #region 私有方法

        private void EnsureProvider()
        {
            if (_resourceProvider == null)
            {
                throw new InvalidOperationException($"[{Name}] ResourceProvider未初始化");
            }
        }

        #endregion

        /// <summary>
        /// 关闭Module
        /// </summary>
        protected override UniTask OnShutdownAsync()
        {
            _resourceProvider = null;
            Log($"[{Name}] 资源模块已关闭");
            return base.OnShutdownAsync();
        }
    }
}

