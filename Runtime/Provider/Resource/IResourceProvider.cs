using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace JulyCore.Provider.Resource
{
    public enum JPlayMode
    {
        /// <summary>
        /// 编辑器下的模拟模式
        /// </summary>
        EditorSimulateMode,

        /// <summary>
        /// 离线运行模式
        /// </summary>
        OfflinePlayMode,

        /// <summary>
        /// 联机运行模式
        /// </summary>
        HostPlayMode,

        /// <summary>
        /// WebGL运行模式
        /// </summary>
        WebPlayMode,

        /// <summary>
        /// 自定义运行模式
        /// </summary>
        CustomPlayMode,
    }
    
    /// <summary>
    /// 资源提供者接口
    /// 提供Unity资源加载能力，支持引用计数管理
    /// 覆盖传统手游常见功能：资源加载、子资源加载、资源检查等
    /// 【引用计数管理】
    /// - LoadAsync：手动管理引用计数，需要配对调用 Unload
    /// - LoadWithHandleAsync：自动管理引用计数，Handle 释放时自动 Unload（推荐）
    /// </summary>
    public interface IResourceProvider : Core.IProvider
    {
        #region 手动引用计数管理

        /// <summary>
        /// 异步加载资源（手动管理引用计数）
        /// 使用引用计数管理，多次加载同一资源会增加引用计数
        /// 注意：必须配对调用 Unload，否则会导致资源泄漏
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="fileName">资源文件名（不含扩展名，如 "prefab_name"）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>资源对象，加载失败返回null</returns>
        UniTask<T> LoadAsync<T>(string fileName, CancellationToken cancellationToken = default) where T : Object;

        #endregion

        #region 自动引用计数管理（推荐）

        /// <summary>
        /// 异步加载资源并返回句柄（自动管理引用计数，推荐使用）
        /// 句柄释放时自动减少引用计数，支持 using 语句和 GameObject 绑定
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="fileName">资源文件名</param>
        /// <param name="captureStackTrace">是否捕获调用栈（用于泄漏定位，影响性能）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>资源句柄，加载失败返回null</returns>
        /// <example>
        /// // 方式1：using 语句
        /// using (var handle = await provider.LoadWithHandleAsync<Sprite>("icon"))
        /// {
        ///     var sprite = handle.Asset;
        /// }
        /// 
        /// // 方式2：绑定到 GameObject
        /// var handle = await provider.LoadWithHandleAsync<Sprite>("icon");
        /// handle.BindTo(gameObject); // GameObject 销毁时自动释放
        /// </example>
        UniTask<ResourceHandle<T>> LoadWithHandleAsync<T>(string fileName, bool captureStackTrace = false, CancellationToken cancellationToken = default) where T : Object;

        #endregion

        /// <summary>
        /// 预加载资源（不增加引用计数，仅将资源加载到内存）
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="fileName">资源文件名（不含扩展名）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否预加载成功</returns>
        UniTask<bool> PreloadAsync<T>(string fileName, CancellationToken cancellationToken = default) where T : Object;

        /// <summary>
        /// 批量加载资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="fileNames">资源文件名列表（不含扩展名）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>资源对象列表，加载失败的资源为null</returns>
        UniTask<List<T>> LoadBatchAsync<T>(IEnumerable<string> fileNames, CancellationToken cancellationToken = default) where T : Object;

        /// <summary>
        /// 加载子资源（如SpriteAtlas中的Sprite、AudioClip等）
        /// </summary>
        /// <typeparam name="T">子资源类型</typeparam>
        /// <param name="fileName">资源文件名（不含扩展名）</param>
        /// <param name="assetName">子资源名称</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>子资源对象，加载失败返回null</returns>
        UniTask<T> LoadSubAssetAsync<T>(string fileName, string assetName, CancellationToken cancellationToken = default) where T : Object;

        /// <summary>
        /// 批量加载子资源
        /// </summary>
        /// <typeparam name="T">子资源类型</typeparam>
        /// <param name="fileName">资源文件名（不含扩展名）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>所有子资源列表</returns>
        UniTask<List<T>> LoadAllSubAssetsAsync<T>(string fileName, CancellationToken cancellationToken = default) where T : Object;

        /// <summary>
        /// 检查资源是否存在
        /// </summary>
        /// <param name="fileName">资源文件名（不含扩展名）</param>
        /// <returns>是否存在</returns>
        bool HasAsset(string fileName);

        /// <summary>
        /// 卸载资源（减少引用计数，计数为0时真正释放）
        /// </summary>
        /// <param name="obj">要卸载的资源对象</param>
        void Unload(Object obj);

        /// <summary>
        /// 卸载所有资源
        /// </summary>
        void UnloadAll();

        /// <summary>
        /// 获取资源的引用计数
        /// </summary>
        /// <param name="obj">资源对象</param>
        /// <returns>引用计数，如果资源不存在返回0</returns>
        int GetRefCount(Object obj);

        /// <summary>
        /// 获取资源使用统计信息
        /// </summary>
        /// <returns>资源统计信息</returns>
        ResourceStatistics GetStatistics();

        /// <summary>
        /// 检测资源泄漏
        /// </summary>
        /// <param name="leakThreshold">泄漏阈值（引用计数超过此值视为可能泄漏）</param>
        /// <returns>泄漏资源列表</returns>
        List<ResourceLeakInfo> DetectLeaks(int leakThreshold = 10);

        /// <summary>
        /// 获取活跃的资源句柄信息（用于泄漏检测）
        /// </summary>
        /// <returns>活跃句柄列表</returns>
        List<ActiveHandleInfo> GetActiveHandles();

        /// <summary>
        /// 获取所有已加载资源的详细信息
        /// </summary>
        /// <returns>资源信息列表</returns>
        List<ResourceInfo> GetAllResources();

        /// <summary>
        /// 获取资源使用历史
        /// </summary>
        /// <param name="location">资源位置（可选，为空则返回所有资源的历史）</param>
        /// <param name="maxCount">最大返回数量</param>
        /// <returns>资源使用历史列表</returns>
        List<ResourceUsageHistory> GetResourceUsageHistory(string location = null, int maxCount = 100);

        /// <summary>
        /// 获取资源使用频率统计
        /// </summary>
        /// <param name="topN">返回前N个最常使用的资源</param>
        /// <returns>资源使用频率列表</returns>
        List<ResourceUsageFrequency> GetResourceUsageFrequency(int topN = 20);

        #region 场景加载

        /// <summary>
        /// 异步加载场景
        /// </summary>
        /// <param name="sceneName">场景名称</param>
        /// <param name="loadSceneMode">加载模式（单场景或叠加）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>加载的场景</returns>
        UniTask<UnityEngine.SceneManagement.Scene> LoadSceneAsync(
            string sceneName, 
            UnityEngine.SceneManagement.LoadSceneMode loadSceneMode = UnityEngine.SceneManagement.LoadSceneMode.Single, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步卸载场景
        /// </summary>
        /// <param name="sceneName">场景名称</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否卸载成功</returns>
        UniTask<bool> UnloadSceneAsync(string sceneName, CancellationToken cancellationToken = default);

        #endregion
    }

    /// <summary>
    /// 资源使用历史记录
    /// </summary>
    public class ResourceUsageHistory
    {
        /// <summary>
        /// 资源位置/名称
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// 操作类型（Load/Unload）
        /// </summary>
        public string Operation { get; set; }

        /// <summary>
        /// 操作时间
        /// </summary>
        public System.DateTime Timestamp { get; set; }

        /// <summary>
        /// 操作后的引用计数
        /// </summary>
        public int RefCount { get; set; }
    }

    /// <summary>
    /// 资源使用频率统计
    /// </summary>
    public class ResourceUsageFrequency
    {
        /// <summary>
        /// 资源位置/名称
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// 加载次数
        /// </summary>
        public int LoadCount { get; set; }

        /// <summary>
        /// 卸载次数
        /// </summary>
        public int UnloadCount { get; set; }

        /// <summary>
        /// 总使用次数
        /// </summary>
        public int TotalUsageCount => LoadCount + UnloadCount;

        /// <summary>
        /// 最后使用时间
        /// </summary>
        public System.DateTime LastUsedTime { get; set; }
    }

    /// <summary>
    /// 资源统计信息
    /// </summary>
    public class ResourceStatistics
    {
        /// <summary>
        /// 已加载资源总数
        /// </summary>
        public int TotalResources { get; set; }

        /// <summary>
        /// 总引用计数
        /// </summary>
        public int TotalRefCount { get; set; }

        /// <summary>
        /// 活跃Handle数量
        /// </summary>
        public int ActiveHandles { get; set; }

        /// <summary>
        /// 预加载资源数量
        /// </summary>
        public int PreloadedResources { get; set; }

        /// <summary>
        /// 缓存资源数量
        /// </summary>
        public int CachedResources { get; set; }
    }

    /// <summary>
    /// 资源泄漏信息
    /// </summary>
    public class ResourceLeakInfo
    {
        /// <summary>
        /// 资源位置/名称
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// 资源对象名称
        /// </summary>
        public string ResourceName { get; set; }

        /// <summary>
        /// 引用计数
        /// </summary>
        public int RefCount { get; set; }

        /// <summary>
        /// 资源类型
        /// </summary>
        public string ResourceType { get; set; }
    }

    /// <summary>
    /// 活跃句柄信息（用于泄漏检测）
    /// </summary>
    public class ActiveHandleInfo
    {
        /// <summary>
        /// 资源路径
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// 资源类型
        /// </summary>
        public string ResourceType { get; set; }

        /// <summary>
        /// 加载时间
        /// </summary>
        public System.DateTime LoadTime { get; set; }

        /// <summary>
        /// 存活时长（秒）
        /// </summary>
        public double AliveSeconds { get; set; }

        /// <summary>
        /// 加载调用栈
        /// </summary>
        public string LoadStackTrace { get; set; }

        /// <summary>
        /// 是否绑定到 GameObject
        /// </summary>
        public bool IsBoundToObject { get; set; }

        /// <summary>
        /// 绑定的 GameObject 名称
        /// </summary>
        public string BoundObjectName { get; set; }
    }

    /// <summary>
    /// 资源详细信息
    /// </summary>
    public class ResourceInfo
    {
        /// <summary>
        /// 资源位置/名称
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// 资源对象名称
        /// </summary>
        public string ResourceName { get; set; }

        /// <summary>
        /// 引用计数
        /// </summary>
        public int RefCount { get; set; }

        /// <summary>
        /// 资源类型
        /// </summary>
        public string ResourceType { get; set; }

        /// <summary>
        /// 是否在缓存中
        /// </summary>
        public bool IsCached { get; set; }

        /// <summary>
        /// Handle数量
        /// </summary>
        public int HandleCount { get; set; }
    }
}
