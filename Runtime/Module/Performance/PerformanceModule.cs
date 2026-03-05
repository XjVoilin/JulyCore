using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Module.Base;
using JulyCore.Provider.Performance;

namespace JulyCore.Module.Performance
{
    /// <summary>
    /// 性能监控模块
    /// 业务语义与流程调度层：决定告警触发条件、报告生成规则
    /// 管理性能监控状态和业务逻辑
    /// 不直接操作底层性能API，不负责性能数据采集
    /// </summary>
    internal class PerformanceModule : ModuleBase
    {
        private IPerformanceProvider _performanceProvider;

        protected override LogChannel LogChannel => LogChannel.Performance;

        // 业务状态：告警配置
        private float _fpsAlertThreshold = 0f;
        private Action<float> _fpsAlertCallback;
        private bool _fpsAlertTriggered = false;

        private float _memoryAlertThreshold = 0f;
        private Action<float> _memoryAlertCallback;
        private bool _memoryAlertTriggered = false;

        /// <summary>
        /// 模块执行优先级
        /// </summary>
        public override int Priority => Frameworkconst.PriorityPerformanceModule;

        protected override UniTask OnInitAsync()
        {
            try
            {
                _performanceProvider = GetProvider<IPerformanceProvider>();
                if (_performanceProvider == null)
                {
                    throw new JulyException($"[{Name}] 未找到IPerformanceProvider，请先注册PerformanceProvider");
                }

                Log($"[{Name}] 性能监控模块初始化完成");
                return base.OnInitAsync();
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 性能监控模块初始化失败: {ex.Message}");
                throw;
            }
        }

        protected override void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            // 更新性能数据采集（技术层）
            _performanceProvider?.Update(elapseSeconds);

            // 业务规则：检查告警
            CheckAlerts();
        }

        #region FPS 监控

        /// <summary>
        /// 当前FPS
        /// </summary>
        internal float CurrentFPS => _performanceProvider?.CurrentFPS ?? 0f;

        /// <summary>
        /// 平均FPS
        /// </summary>
        internal float AverageFPS => _performanceProvider?.AverageFPS ?? 0f;

        /// <summary>
        /// 最低FPS
        /// </summary>
        internal float MinFPS => _performanceProvider?.MinFPS ?? 0f;

        /// <summary>
        /// 最高FPS
        /// </summary>
        internal float MaxFPS => _performanceProvider?.MaxFPS ?? 0f;

        /// <summary>
        /// 获取FPS历史数据
        /// </summary>
        /// <param name="count">数据点数量</param>
        /// <returns>FPS数据列表</returns>
        internal List<float> GetFPSHistory(int count = 60)
        {
            EnsureProvider();
            return _performanceProvider.GetFPSHistory(count);
        }

        #endregion

        #region 内存监控

        /// <summary>
        /// 当前总内存使用（MB）
        /// </summary>
        internal float TotalMemoryMB => _performanceProvider?.TotalMemoryMB ?? 0f;

        /// <summary>
        /// 已分配内存（MB）
        /// </summary>
        internal float AllocatedMemoryMB => _performanceProvider?.AllocatedMemoryMB ?? 0f;

        /// <summary>
        /// 保留内存（MB）
        /// </summary>
        internal float ReservedMemoryMB => _performanceProvider?.ReservedMemoryMB ?? 0f;

        /// <summary>
        /// Mono堆内存（MB）
        /// </summary>
        internal float MonoHeapSizeMB => _performanceProvider?.MonoHeapSizeMB ?? 0f;

        /// <summary>
        /// Mono已使用内存（MB）
        /// </summary>
        internal float MonoUsedSizeMB => _performanceProvider?.MonoUsedSizeMB ?? 0f;

        /// <summary>
        /// GC总分配内存（MB）
        /// </summary>
        internal float GCTotalAllocatedMB => _performanceProvider?.GCTotalAllocatedMB ?? 0f;

        /// <summary>
        /// 获取GC统计信息
        /// </summary>
        /// <returns>GC统计信息</returns>
        internal GCStatistics GetGCStatistics()
        {
            EnsureProvider();
            return _performanceProvider.GetGCStatistics();
        }

        /// <summary>
        /// 保存性能数据到文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否保存成功</returns>
        internal async UniTask<bool> SavePerformanceDataAsync(string filePath, CancellationToken cancellationToken = default)
        {
            EnsureProvider();
            return await _performanceProvider.SavePerformanceDataAsync(filePath, cancellationToken);
        }

        /// <summary>
        /// 从文件加载性能数据
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>性能数据，加载失败返回null</returns>
        internal async UniTask<PerformanceDataSnapshot> LoadPerformanceDataAsync(string filePath, CancellationToken cancellationToken = default)
        {
            EnsureProvider();
            return await _performanceProvider.LoadPerformanceDataAsync(filePath, cancellationToken);
        }

        /// <summary>
        /// 获取性能数据历史记录
        /// </summary>
        /// <param name="maxCount">最大返回数量</param>
        /// <returns>性能数据快照列表</returns>
        internal List<PerformanceDataSnapshot> GetPerformanceDataHistory(int maxCount = 100)
        {
            EnsureProvider();
            return _performanceProvider.GetPerformanceDataHistory(maxCount);
        }

        /// <summary>
        /// 清除性能数据历史记录
        /// </summary>
        internal void ClearPerformanceDataHistory()
        {
            EnsureProvider();
            _performanceProvider.ClearPerformanceDataHistory();
        }

        /// <summary>
        /// 获取内存快照
        /// </summary>
        /// <returns>内存快照数据</returns>
        internal MemorySnapshot GetMemorySnapshot()
        {
            EnsureProvider();
            return _performanceProvider.GetMemorySnapshot();
        }

        /// <summary>
        /// 强制GC
        /// </summary>
        internal void ForceGC()
        {
            EnsureProvider();
            _performanceProvider.ForceGC();
        }

        #endregion

        #region CPU 性能分析

        /// <summary>
        /// 开始性能分析
        /// </summary>
        /// <param name="sampleName">采样名称</param>
        internal void BeginSample(string sampleName)
        {
            EnsureProvider();
            _performanceProvider.BeginSample(sampleName);
        }

        /// <summary>
        /// 结束性能分析
        /// </summary>
        /// <param name="sampleName">采样名称</param>
        internal void EndSample(string sampleName)
        {
            EnsureProvider();
            _performanceProvider.EndSample(sampleName);
        }

        /// <summary>
        /// 获取性能采样数据
        /// </summary>
        /// <returns>性能采样数据字典</returns>
        internal Dictionary<string, PerformanceSample> GetSamples()
        {
            EnsureProvider();
            return _performanceProvider.GetSamples();
        }

        /// <summary>
        /// 清除所有采样数据
        /// </summary>
        internal void ClearSamples()
        {
            EnsureProvider();
            _performanceProvider.ClearSamples();
        }

        #endregion

        #region 资源监控

        /// <summary>
        /// 获取资源使用统计
        /// </summary>
        /// <returns>资源统计信息</returns>
        internal ResourceStatistics GetResourceStatistics()
        {
            EnsureProvider();
            return _performanceProvider.GetResourceStatistics();
        }

        #endregion

        #region 性能告警（业务规则）

        /// <summary>
        /// 设置FPS告警阈值（业务规则）
        /// </summary>
        /// <param name="threshold">FPS阈值</param>
        /// <param name="callback">告警回调</param>
        internal void SetFPSAlert(float threshold, Action<float> callback)
        {
            _fpsAlertThreshold = threshold;
            _fpsAlertCallback = callback;
            _fpsAlertTriggered = false;
        }

        /// <summary>
        /// 设置内存告警阈值（业务规则）
        /// </summary>
        /// <param name="thresholdMB">内存阈值（MB）</param>
        /// <param name="callback">告警回调</param>
        internal void SetMemoryAlert(float thresholdMB, Action<float> callback)
        {
            _memoryAlertThreshold = thresholdMB;
            _memoryAlertCallback = callback;
            _memoryAlertTriggered = false;
        }

        /// <summary>
        /// 清除FPS告警（业务规则）
        /// </summary>
        internal void ClearFPSAlert()
        {
            _fpsAlertThreshold = 0f;
            _fpsAlertCallback = null;
            _fpsAlertTriggered = false;
        }

        /// <summary>
        /// 清除内存告警（业务规则）
        /// </summary>
        internal void ClearMemoryAlert()
        {
            _memoryAlertThreshold = 0f;
            _memoryAlertCallback = null;
            _memoryAlertTriggered = false;
        }

        /// <summary>
        /// 检查告警（业务规则：告警触发条件）
        /// </summary>
        private void CheckAlerts()
        {
            EnsureProvider();

            // 检查FPS告警
            if (_fpsAlertThreshold > 0f && _fpsAlertCallback != null)
            {
                var currentFPS = _performanceProvider.CurrentFPS;
                if (currentFPS < _fpsAlertThreshold && !_fpsAlertTriggered)
                {
                    _fpsAlertTriggered = true;
                    _fpsAlertCallback?.Invoke(currentFPS);
                }
                else if (currentFPS >= _fpsAlertThreshold)
                {
                    _fpsAlertTriggered = false;
                }
            }

            // 检查内存告警
            if (_memoryAlertThreshold > 0f && _memoryAlertCallback != null)
            {
                var currentMemory = _performanceProvider.TotalMemoryMB;
                if (currentMemory > _memoryAlertThreshold && !_memoryAlertTriggered)
                {
                    _memoryAlertTriggered = true;
                    _memoryAlertCallback?.Invoke(currentMemory);
                }
                else if (currentMemory <= _memoryAlertThreshold)
                {
                    _memoryAlertTriggered = false;
                }
            }
        }

        #endregion

        #region 性能报告（业务规则）

        /// <summary>
        /// 生成性能报告（业务规则：报告生成策略）
        /// </summary>
        /// <returns>性能报告字符串</returns>
        internal string GenerateReport()
        {
            EnsureProvider();

            var report = new StringBuilder();
            report.AppendLine("=== 性能监控报告 ===");
            report.AppendLine($"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine();

            // FPS信息
            report.AppendLine("--- FPS 信息 ---");
            report.AppendLine($"当前FPS: {_performanceProvider.CurrentFPS:F2}");
            report.AppendLine($"平均FPS: {_performanceProvider.AverageFPS:F2}");
            report.AppendLine($"最低FPS: {_performanceProvider.MinFPS:F2}");
            report.AppendLine($"最高FPS: {_performanceProvider.MaxFPS:F2}");
            report.AppendLine();

            // 内存信息
            var memory = _performanceProvider.GetMemorySnapshot();
            report.AppendLine("--- 内存信息 ---");
            report.AppendLine($"总内存: {memory.TotalMemoryMB:F2} MB");
            report.AppendLine($"已分配内存: {memory.AllocatedMemoryMB:F2} MB");
            report.AppendLine($"保留内存: {memory.ReservedMemoryMB:F2} MB");
            report.AppendLine($"Mono堆内存: {memory.MonoHeapSizeMB:F2} MB");
            report.AppendLine($"Mono已使用: {memory.MonoUsedSizeMB:F2} MB");
            report.AppendLine($"GC总分配: {memory.GCTotalAllocatedMB:F2} MB");
            
            // GC统计信息
            var gcStats = _performanceProvider.GetGCStatistics();
            report.AppendLine("--- GC 统计信息 ---");
            report.AppendLine($"GC总分配内存: {gcStats.TotalAllocatedMB:F2} MB");
            report.AppendLine($"Mono堆大小: {gcStats.MonoHeapSizeMB:F2} MB");
            report.AppendLine($"Mono已使用: {gcStats.MonoUsedSizeMB:F2} MB");
            report.AppendLine($"GC收集次数: {gcStats.GCCount}");
            report.AppendLine($"GC频率: {gcStats.GCFrequency:F4} 次/秒");
            report.AppendLine($"上次GC时间: {gcStats.LastGCTime:F2} 秒");
            report.AppendLine();

            // 资源信息
            var resources = _performanceProvider.GetResourceStatistics();
            report.AppendLine("--- 资源信息 ---");
            report.AppendLine($"纹理数量: {resources.TextureCount}");
            report.AppendLine($"纹理内存: {resources.TextureMemoryMB:F2} MB");
            report.AppendLine($"音频数量: {resources.AudioClipCount}");
            report.AppendLine($"音频内存: {resources.AudioClipMemoryMB:F2} MB");
            report.AppendLine($"网格数量: {resources.MeshCount}");
            report.AppendLine($"网格内存: {resources.MeshMemoryMB:F2} MB");
            report.AppendLine($"材质数量: {resources.MaterialCount}");
            report.AppendLine($"GameObject数量: {resources.GameObjectCount}");
            report.AppendLine();

            // 性能采样
            var samples = _performanceProvider.GetSamples();
            if (samples.Count > 0)
            {
                report.AppendLine("--- 性能采样 ---");
                foreach (var kvp in samples.OrderByDescending(s => s.Value.TotalTime))
                {
                    var sample = kvp.Value;
                    report.AppendLine($"{sample.Name}:");
                    report.AppendLine($"  调用次数: {sample.CallCount}");
                    report.AppendLine($"  总耗时: {sample.TotalTime:F4} ms");
                    report.AppendLine($"  平均耗时: {sample.AverageTime:F4} ms");
                    report.AppendLine($"  最小耗时: {sample.MinTime:F4} ms");
                    report.AppendLine($"  最大耗时: {sample.MaxTime:F4} ms");
                }
            }

            return report.ToString();
        }

        /// <summary>
        /// 导出性能数据（业务规则：JSON格式生成策略）
        /// </summary>
        /// <returns>JSON字符串</returns>
        internal string ExportData()
        {
            EnsureProvider();

            var memory = _performanceProvider.GetMemorySnapshot();
            var resources = _performanceProvider.GetResourceStatistics();
            var samples = _performanceProvider.GetSamples();
            var fpsHistory = _performanceProvider.GetFPSHistory(60);

            var json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine($"  \"timestamp\": \"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\",");
            json.AppendLine("  \"fps\": {");
            json.AppendLine($"    \"current\": {_performanceProvider.CurrentFPS:F2},");
            json.AppendLine($"    \"average\": {_performanceProvider.AverageFPS:F2},");
            json.AppendLine($"    \"min\": {_performanceProvider.MinFPS:F2},");
            json.AppendLine($"    \"max\": {_performanceProvider.MaxFPS:F2},");
            var fpsHistoryStr = fpsHistory.Count > 0
                ? string.Join(", ", fpsHistory.Select(f => f.ToString("F2")))
                : "";
            json.AppendLine($"    \"history\": [{fpsHistoryStr}]");
            json.AppendLine("  },");
            json.AppendLine("  \"memory\": {");
            json.AppendLine($"    \"totalMB\": {memory.TotalMemoryMB:F2},");
            json.AppendLine($"    \"allocatedMB\": {memory.AllocatedMemoryMB:F2},");
            json.AppendLine($"    \"reservedMB\": {memory.ReservedMemoryMB:F2},");
            json.AppendLine($"    \"monoHeapSizeMB\": {memory.MonoHeapSizeMB:F2},");
            json.AppendLine($"    \"monoUsedSizeMB\": {memory.MonoUsedSizeMB:F2},");
            json.AppendLine($"    \"gcTotalAllocatedMB\": {memory.GCTotalAllocatedMB:F2}");
            json.AppendLine("  },");
            
            // GC统计信息
            var gcStats = _performanceProvider.GetGCStatistics();
            json.AppendLine("  \"gc\": {");
            json.AppendLine($"    \"totalAllocatedMB\": {gcStats.TotalAllocatedMB:F2},");
            json.AppendLine($"    \"monoHeapSizeMB\": {gcStats.MonoHeapSizeMB:F2},");
            json.AppendLine($"    \"monoUsedSizeMB\": {gcStats.MonoUsedSizeMB:F2},");
            json.AppendLine($"    \"gcCount\": {gcStats.GCCount},");
            json.AppendLine($"    \"gcFrequency\": {gcStats.GCFrequency:F4},");
            json.AppendLine($"    \"lastGCTime\": {gcStats.LastGCTime:F2}");
            json.AppendLine("  },");
            json.AppendLine("  \"resources\": {");
            json.AppendLine($"    \"textureCount\": {resources.TextureCount},");
            json.AppendLine($"    \"textureMemoryMB\": {resources.TextureMemoryMB:F2},");
            json.AppendLine($"    \"audioClipCount\": {resources.AudioClipCount},");
            json.AppendLine($"    \"audioClipMemoryMB\": {resources.AudioClipMemoryMB:F2},");
            json.AppendLine($"    \"meshCount\": {resources.MeshCount},");
            json.AppendLine($"    \"meshMemoryMB\": {resources.MeshMemoryMB:F2},");
            json.AppendLine($"    \"materialCount\": {resources.MaterialCount},");
            json.AppendLine($"    \"gameObjectCount\": {resources.GameObjectCount}");
            json.AppendLine("  }");

            if (samples.Count > 0)
            {
                json.AppendLine("  ,\"samples\": [");
                var sampleList = samples.Values.ToList();
                for (int i = 0; i < sampleList.Count; i++)
                {
                    var sample = sampleList[i];
                    json.AppendLine("    {");
                    json.AppendLine($"      \"name\": \"{sample.Name}\",");
                    json.AppendLine($"      \"callCount\": {sample.CallCount},");
                    json.AppendLine($"      \"totalTime\": {sample.TotalTime:F4},");
                    json.AppendLine($"      \"averageTime\": {sample.AverageTime:F4},");
                    json.AppendLine($"      \"minTime\": {sample.MinTime:F4},");
                    json.AppendLine($"      \"maxTime\": {sample.MaxTime:F4}");
                    json.Append(i < sampleList.Count - 1 ? "    }," : "    }");
                    json.AppendLine();
                }
                json.AppendLine("  ]");
            }

            json.AppendLine("}");
            return json.ToString();
        }

        #endregion

        private void EnsureProvider()
        {
            if (_performanceProvider == null)
            {
                throw new InvalidOperationException($"[{Name}] PerformanceProvider未初始化");
            }
        }
    }
}

