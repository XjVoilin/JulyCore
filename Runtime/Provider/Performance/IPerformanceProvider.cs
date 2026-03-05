using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;

namespace JulyCore.Provider.Performance
{
    /// <summary>
    /// 性能监控提供者接口
    /// 纯技术执行层：负责性能数据采集、底层性能API调用
    /// 不包含任何业务语义，不维护业务状态
    /// 所有业务逻辑（告警、报告生成）由Module层处理
    /// </summary>
    public interface IPerformanceProvider : IProvider
    {
        #region FPS 监控

        /// <summary>
        /// 当前FPS
        /// </summary>
        float CurrentFPS { get; }

        /// <summary>
        /// 平均FPS
        /// </summary>
        float AverageFPS { get; }

        /// <summary>
        /// 最低FPS
        /// </summary>
        float MinFPS { get; }

        /// <summary>
        /// 最高FPS
        /// </summary>
        float MaxFPS { get; }

        /// <summary>
        /// 获取FPS历史数据（用于绘制曲线）
        /// </summary>
        /// <param name="count">获取的数据点数量</param>
        /// <returns>FPS数据列表</returns>
        List<float> GetFPSHistory(int count = 60);

        #endregion

        #region 内存监控

        /// <summary>
        /// 当前总内存使用（MB）
        /// </summary>
        float TotalMemoryMB { get; }

        /// <summary>
        /// 已分配内存（MB）
        /// </summary>
        float AllocatedMemoryMB { get; }

        /// <summary>
        /// 保留内存（MB）
        /// </summary>
        float ReservedMemoryMB { get; }

        /// <summary>
        /// Mono堆内存（MB）
        /// </summary>
        float MonoHeapSizeMB { get; }

        /// <summary>
        /// Mono已使用内存（MB）
        /// </summary>
        float MonoUsedSizeMB { get; }

        /// <summary>
        /// GC总分配内存（MB）
        /// </summary>
        float GCTotalAllocatedMB { get; }

        /// <summary>
        /// 获取GC统计信息
        /// </summary>
        /// <returns>GC统计信息</returns>
        GCStatistics GetGCStatistics();

        /// <summary>
        /// 获取内存快照
        /// </summary>
        /// <returns>内存快照数据</returns>
        MemorySnapshot GetMemorySnapshot();

        /// <summary>
        /// 强制GC
        /// </summary>
        void ForceGC();

        #endregion

        #region CPU 性能分析

        /// <summary>
        /// 开始性能分析
        /// </summary>
        /// <param name="sampleName">采样名称</param>
        void BeginSample(string sampleName);

        /// <summary>
        /// 结束性能分析
        /// </summary>
        /// <param name="sampleName">采样名称</param>
        void EndSample(string sampleName);

        /// <summary>
        /// 获取性能采样数据
        /// </summary>
        /// <returns>性能采样数据字典</returns>
        Dictionary<string, PerformanceSample> GetSamples();

        /// <summary>
        /// 清除所有采样数据
        /// </summary>
        void ClearSamples();

        #endregion

        #region 资源监控

        /// <summary>
        /// 获取资源使用统计
        /// </summary>
        /// <returns>资源统计信息</returns>
        ResourceStatistics GetResourceStatistics();

        #endregion


        #region 更新

        /// <summary>
        /// 更新性能监控（每帧调用）
        /// </summary>
        /// <param name="deltaTime">帧时间</param>
        void Update(float deltaTime);

        #endregion

        #region 性能数据持久化

        /// <summary>
        /// 保存性能数据到文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否保存成功</returns>
        UniTask<bool> SavePerformanceDataAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// 从文件加载性能数据
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>性能数据，加载失败返回null</returns>
        UniTask<PerformanceDataSnapshot> LoadPerformanceDataAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取性能数据历史记录（内存中的历史数据）
        /// </summary>
        /// <param name="maxCount">最大返回数量</param>
        /// <returns>性能数据快照列表</returns>
        List<PerformanceDataSnapshot> GetPerformanceDataHistory(int maxCount = 100);

        /// <summary>
        /// 清除性能数据历史记录
        /// </summary>
        void ClearPerformanceDataHistory();

        #endregion
    }

    /// <summary>
    /// 内存快照
    /// </summary>
    public class MemorySnapshot
    {
        public float TotalMemoryMB { get; set; }
        public float AllocatedMemoryMB { get; set; }
        public float ReservedMemoryMB { get; set; }
        public float MonoHeapSizeMB { get; set; }
        public float MonoUsedSizeMB { get; set; }
        public float GCTotalAllocatedMB { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 性能采样数据
    /// </summary>
    public class PerformanceSample
    {
        public string Name { get; set; }
        public int CallCount { get; set; }
        public float TotalTime { get; set; }
        public float AverageTime { get; set; }
        public float MinTime { get; set; }
        public float MaxTime { get; set; }
    }

    /// <summary>
    /// 资源统计信息
    /// </summary>
    public class ResourceStatistics
    {
        public int TextureCount { get; set; }
        public float TextureMemoryMB { get; set; }
        public int AudioClipCount { get; set; }
        public float AudioClipMemoryMB { get; set; }
        public int MeshCount { get; set; }
        public float MeshMemoryMB { get; set; }
        public int MaterialCount { get; set; }
        public int GameObjectCount { get; set; }
    }

    /// <summary>
    /// GC统计信息
    /// </summary>
    public class GCStatistics
    {
        /// <summary>
        /// GC总分配内存（MB）
        /// </summary>
        public float TotalAllocatedMB { get; set; }

        /// <summary>
        /// 当前Mono堆大小（MB）
        /// </summary>
        public float MonoHeapSizeMB { get; set; }

        /// <summary>
        /// 当前Mono已使用内存（MB）
        /// </summary>
        public float MonoUsedSizeMB { get; set; }

        /// <summary>
        /// GC收集次数（自启动以来）
        /// </summary>
        public int GCCount { get; set; }

        /// <summary>
        /// 上次GC时间（秒，自启动以来）
        /// </summary>
        public float LastGCTime { get; set; }

        /// <summary>
        /// GC频率（次/秒）
        /// </summary>
        public float GCFrequency { get; set; }
    }

    /// <summary>
    /// 性能数据快照
    /// </summary>
    public class PerformanceDataSnapshot
    {
        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// FPS信息
        /// </summary>
        public float CurrentFPS { get; set; }
        public float AverageFPS { get; set; }
        public float MinFPS { get; set; }
        public float MaxFPS { get; set; }

        /// <summary>
        /// 内存信息
        /// </summary>
        public MemorySnapshot Memory { get; set; }

        /// <summary>
        /// GC统计信息
        /// </summary>
        public GCStatistics GC { get; set; }

        /// <summary>
        /// 资源统计信息
        /// </summary>
        public ResourceStatistics Resources { get; set; }
    }
}

