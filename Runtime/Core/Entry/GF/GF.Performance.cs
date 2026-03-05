using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Module.Performance;
using JulyCore.Provider.Performance;

namespace JulyCore
{
    public static partial class GF
    {
        /// <summary>
        /// 性能监控相关操作
        /// </summary>
        public static class Performance
        {
            private static PerformanceModule _module;
            private static PerformanceModule Module
            {
                get
                {
                    _module ??= GetModule<PerformanceModule>();
                    return _module;
                }
            }
            
            #region FPS 监控

            /// <summary>
            /// 当前FPS
            /// </summary>
            public static float CurrentFPS => Module.CurrentFPS;

            /// <summary>
            /// 平均FPS
            /// </summary>
            public static float AverageFPS => Module.AverageFPS;

            /// <summary>
            /// 最低FPS
            /// </summary>
            public static float MinFPS => Module.MinFPS;

            /// <summary>
            /// 最高FPS
            /// </summary>
            public static float MaxFPS => Module.MaxFPS;

            /// <summary>
            /// 获取FPS历史数据（用于绘制曲线）
            /// </summary>
            /// <param name="count">数据点数量</param>
            /// <returns>FPS数据列表</returns>
            public static List<float> GetFPSHistory(int count = 60)
            {
                return Module.GetFPSHistory(count);
            }

            #endregion

            #region 内存监控

            /// <summary>
            /// 当前总内存使用（MB）
            /// </summary>
            public static float TotalMemoryMB => Module.TotalMemoryMB;

            /// <summary>
            /// 已分配内存（MB）
            /// </summary>
            public static float AllocatedMemoryMB => Module.AllocatedMemoryMB;

            /// <summary>
            /// 保留内存（MB）
            /// </summary>
            public static float ReservedMemoryMB => Module.ReservedMemoryMB;

            /// <summary>
            /// Mono堆内存（MB）
            /// </summary>
            public static float MonoHeapSizeMB => Module.MonoHeapSizeMB;

            /// <summary>
            /// Mono已使用内存（MB）
            /// </summary>
            public static float MonoUsedSizeMB => Module.MonoUsedSizeMB;

            /// <summary>
            /// GC总分配内存（MB）
            /// </summary>
            public static float GCTotalAllocatedMB => Module.GCTotalAllocatedMB;

            /// <summary>
            /// 获取GC统计信息
            /// </summary>
            /// <returns>GC统计信息</returns>
            public static GCStatistics GetGCStatistics()
            {
                return Module.GetGCStatistics();
            }

            /// <summary>
            /// 获取内存快照
            /// </summary>
            /// <returns>内存快照数据</returns>
            public static MemorySnapshot GetMemorySnapshot()
            {
                return Module.GetMemorySnapshot();
            }

            /// <summary>
            /// 强制GC
            /// </summary>
            public static void ForceGC()
            {
                Module.ForceGC();
            }

            #endregion

            #region CPU 性能分析

            /// <summary>
            /// 开始性能分析
            /// </summary>
            /// <param name="sampleName">采样名称</param>
            public static void BeginSample(string sampleName)
            {
                Module.BeginSample(sampleName);
            }

            /// <summary>
            /// 结束性能分析
            /// </summary>
            /// <param name="sampleName">采样名称</param>
            public static void EndSample(string sampleName)
            {
                Module.EndSample(sampleName);
            }

            /// <summary>
            /// 获取性能采样数据
            /// </summary>
            /// <returns>性能采样数据字典</returns>
            public static Dictionary<string, PerformanceSample> GetSamples()
            {
                return Module.GetSamples();
            }

            /// <summary>
            /// 清除所有采样数据
            /// </summary>
            public static void ClearSamples()
            {
                Module.ClearSamples();
            }

            #endregion

            #region 资源监控

            /// <summary>
            /// 获取资源使用统计
            /// </summary>
            /// <returns>资源统计信息</returns>
            public static ResourceStatistics GetResourceStatistics()
            {
                return Module.GetResourceStatistics();
            }

            #endregion

            #region 性能告警

            /// <summary>
            /// 设置FPS告警阈值
            /// </summary>
            /// <param name="threshold">FPS阈值</param>
            /// <param name="callback">告警回调</param>
            public static void SetFPSAlert(float threshold, Action<float> callback)
            {
                Module.SetFPSAlert(threshold, callback);
            }

            /// <summary>
            /// 设置内存告警阈值
            /// </summary>
            /// <param name="thresholdMB">内存阈值（MB）</param>
            /// <param name="callback">告警回调</param>
            public static void SetMemoryAlert(float thresholdMB, Action<float> callback)
            {
                Module.SetMemoryAlert(thresholdMB, callback);
            }

            /// <summary>
            /// 清除FPS告警
            /// </summary>
            public static void ClearFPSAlert()
            {
                Module.ClearFPSAlert();
            }

            /// <summary>
            /// 清除内存告警
            /// </summary>
            public static void ClearMemoryAlert()
            {
                Module.ClearMemoryAlert();
            }

            #endregion

            #region 性能报告

            /// <summary>
            /// 生成性能报告
            /// </summary>
            /// <returns>性能报告字符串</returns>
            public static string GenerateReport()
            {
                return Module.GenerateReport();
            }

            /// <summary>
            /// 导出性能数据（JSON格式）
            /// </summary>
            /// <returns>JSON字符串</returns>
            public static string ExportData()
            {
                return Module.ExportData();
            }

            /// <summary>
            /// 保存性能数据到文件
            /// </summary>
            /// <param name="filePath">文件路径</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>是否保存成功</returns>
            public static async UniTask<bool> SavePerformanceDataAsync(string filePath, System.Threading.CancellationToken cancellationToken = default)
            {
                return await Module.SavePerformanceDataAsync(filePath, cancellationToken);
            }

            /// <summary>
            /// 从文件加载性能数据
            /// </summary>
            /// <param name="filePath">文件路径</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>性能数据，加载失败返回null</returns>
            public static async UniTask<PerformanceDataSnapshot> LoadPerformanceDataAsync(string filePath, System.Threading.CancellationToken cancellationToken = default)
            {
                return await Module.LoadPerformanceDataAsync(filePath, cancellationToken);
            }

            /// <summary>
            /// 获取性能数据历史记录
            /// </summary>
            /// <param name="maxCount">最大返回数量，默认100</param>
            /// <returns>性能数据快照列表</returns>
            public static List<PerformanceDataSnapshot> GetPerformanceDataHistory(int maxCount = 100)
            {
                return Module.GetPerformanceDataHistory(maxCount);
            }

            /// <summary>
            /// 清除性能数据历史记录
            /// </summary>
            public static void ClearPerformanceDataHistory()
            {
                Module.ClearPerformanceDataHistory();
            }

            #endregion
        }
    }
}

