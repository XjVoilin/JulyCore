using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Provider.Base;
using UnityEngine;
using UnityEngine.Profiling;

namespace JulyCore.Provider.Performance
{
    /// <summary>
    /// Unity性能监控提供者实现
    /// 纯技术执行层：负责性能数据采集、底层性能API调用
    /// 不包含任何业务语义，不维护业务状态
    /// </summary>
    internal class UnityPerformanceProvider : ProviderBase, IPerformanceProvider
    {
        public override int Priority => Frameworkconst.PriorityPerformanceProvider;
        protected override LogChannel LogChannel => LogChannel.Performance;

        #region FPS 监控

        private readonly Queue<float> _fpsHistory = new Queue<float>();
        private const int MaxFPSHistorySize = 300; // 保留5秒数据（60fps）
        private float _fpsAccumulator = 0f;
        private int _fpsFrameCount = 0;
        private float _fpsUpdateInterval = 0.5f; // 每0.5秒更新一次FPS
        private float _fpsTimer = 0f;
        private float _currentFPS = 0f;
        private float _averageFPS = 0f;
        private float _minFPS = float.MaxValue;
        private float _maxFPS = 0f;
        private readonly List<float> _fpsSamples = new List<float>();

        public float CurrentFPS => _currentFPS;
        public float AverageFPS => _averageFPS;
        public float MinFPS => _minFPS == float.MaxValue ? 0f : _minFPS;
        public float MaxFPS => _maxFPS;

        public List<float> GetFPSHistory(int count = 60)
        {
            lock (_fpsHistory)
            {
                var list = _fpsHistory.ToList();
                if (list.Count <= count)
                {
                    return list;
                }
                return list.Skip(list.Count - count).ToList();
            }
        }

        #endregion

        #region 内存监控

        public float TotalMemoryMB => Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f);
        public float AllocatedMemoryMB => Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f);
        public float ReservedMemoryMB => Profiler.GetTotalReservedMemoryLong() / (1024f * 1024f);
        public float MonoHeapSizeMB => GC.GetTotalMemory(false) / (1024f * 1024f);
        public float MonoUsedSizeMB => Profiler.GetMonoUsedSizeLong() / (1024f * 1024f);
        public float GCTotalAllocatedMB => Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f);

        // GC统计
        private int _lastGCCount = 0;
        private float _lastGCTime = 0f;
        private float _gameStartTime = 0f;
        private readonly object _gcStatsLock = new object();

        // 性能数据历史记录
        private readonly List<PerformanceDataSnapshot> _performanceDataHistory = new List<PerformanceDataSnapshot>();
        private readonly object _performanceDataHistoryLock = new object();
        private const int MaxPerformanceDataHistorySize = 100; // 最多保留100条历史记录
        private float _lastSnapshotTime = 0f;
        private const float SnapshotInterval = 5f; // 每5秒记录一次快照

        public GCStatistics GetGCStatistics()
        {
            lock (_gcStatsLock)
            {
                var currentGCCount = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
                var currentTime = UnityEngine.Time.realtimeSinceStartup;
                
                // 计算GC频率
                float gcFrequency = 0f;
                if (currentTime > _gameStartTime && currentGCCount > 0)
                {
                    gcFrequency = currentGCCount / (currentTime - _gameStartTime);
                }

                return new GCStatistics
                {
                    TotalAllocatedMB = GCTotalAllocatedMB,
                    MonoHeapSizeMB = MonoHeapSizeMB,
                    MonoUsedSizeMB = MonoUsedSizeMB,
                    GCCount = currentGCCount,
                    LastGCTime = _lastGCTime,
                    GCFrequency = gcFrequency
                };
            }
        }

        public MemorySnapshot GetMemorySnapshot()
        {
            return new MemorySnapshot
            {
                TotalMemoryMB = TotalMemoryMB,
                AllocatedMemoryMB = AllocatedMemoryMB,
                ReservedMemoryMB = ReservedMemoryMB,
                MonoHeapSizeMB = MonoHeapSizeMB,
                MonoUsedSizeMB = MonoUsedSizeMB,
                GCTotalAllocatedMB = GCTotalAllocatedMB,
                Timestamp = DateTime.Now
            };
        }

        public void ForceGC()
        {
            GC.Collect();
            Resources.UnloadUnusedAssets();
        }

        #endregion

        #region CPU 性能分析

        private readonly Dictionary<string, PerformanceSample> _samples = new Dictionary<string, PerformanceSample>();
        private readonly Dictionary<string, float> _sampleStartTimes = new Dictionary<string, float>();
        private readonly object _sampleLock = new object();

        public void BeginSample(string sampleName)
        {
            if (string.IsNullOrEmpty(sampleName))
            {
                return;
            }

            lock (_sampleLock)
            {
                _sampleStartTimes[sampleName] = UnityEngine.Time.realtimeSinceStartup;
            }
        }

        public void EndSample(string sampleName)
        {
            if (string.IsNullOrEmpty(sampleName))
            {
                return;
            }

            lock (_sampleLock)
            {
                if (!_sampleStartTimes.TryGetValue(sampleName, out var startTime))
                {
                    return;
                }

                var elapsed = UnityEngine.Time.realtimeSinceStartup - startTime;
                _sampleStartTimes.Remove(sampleName);

                if (!_samples.TryGetValue(sampleName, out var sample))
                {
                    sample = new PerformanceSample
                    {
                        Name = sampleName,
                        MinTime = elapsed,
                        MaxTime = elapsed
                    };
                    _samples[sampleName] = sample;
                }

                sample.CallCount++;
                sample.TotalTime += elapsed;
                sample.AverageTime = sample.TotalTime / sample.CallCount;
                sample.MinTime = Mathf.Min(sample.MinTime, elapsed);
                sample.MaxTime = Mathf.Max(sample.MaxTime, elapsed);
            }
        }

        public Dictionary<string, PerformanceSample> GetSamples()
        {
            lock (_sampleLock)
            {
                return new Dictionary<string, PerformanceSample>(_samples);
            }
        }

        public void ClearSamples()
        {
            lock (_sampleLock)
            {
                _samples.Clear();
                _sampleStartTimes.Clear();
            }
        }

        #endregion

        #region 资源监控

        public ResourceStatistics GetResourceStatistics()
        {
            var stats = new ResourceStatistics();

            // 统计纹理
            var textures = Resources.FindObjectsOfTypeAll<Texture>();
            stats.TextureCount = textures.Length;
            long textureMemory = 0;
            foreach (var t in textures)
            {
                textureMemory += Profiler.GetRuntimeMemorySizeLong(t);
            }
            stats.TextureMemoryMB = textureMemory / (1024f * 1024f);

            // 统计音频
            var audioClips = Resources.FindObjectsOfTypeAll<AudioClip>();
            stats.AudioClipCount = audioClips.Length;
            long audioMemory = 0;
            foreach (var a in audioClips)
            {
                audioMemory += Profiler.GetRuntimeMemorySizeLong(a);
            }
            stats.AudioClipMemoryMB = audioMemory / (1024f * 1024f);

            // 统计网格
            var meshes = Resources.FindObjectsOfTypeAll<Mesh>();
            stats.MeshCount = meshes.Length;
            long meshMemory = 0;
            foreach (var m in meshes)
            {
                meshMemory += Profiler.GetRuntimeMemorySizeLong(m);
            }
            stats.MeshMemoryMB = meshMemory / (1024f * 1024f);

            // 统计材质
            var materials = Resources.FindObjectsOfTypeAll<Material>();
            stats.MaterialCount = materials.Length;

            // 统计GameObject
            stats.GameObjectCount = UnityEngine.Object.FindObjectsOfType<GameObject>().Length;

            return stats;
        }

        #endregion

        #region 初始化

        protected override UniTask OnInitAsync()
        {
            _gameStartTime = UnityEngine.Time.realtimeSinceStartup;
            _lastGCCount = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
            return base.OnInitAsync();
        }

        #endregion

        #region 更新

        public void Update(float deltaTime)
        {
            // 更新FPS（技术层数据采集）
            _fpsAccumulator += deltaTime;
            _fpsFrameCount++;
            _fpsTimer += deltaTime;

            if (_fpsTimer >= _fpsUpdateInterval)
            {
                _currentFPS = _fpsFrameCount / _fpsTimer;
                _fpsTimer = 0f;
                _fpsFrameCount = 0;

                // 更新统计数据
                _fpsSamples.Add(_currentFPS);
                if (_fpsSamples.Count > 100) // 保留最近100个样本
                {
                    _fpsSamples.RemoveAt(0);
                }

                _averageFPS = _fpsSamples.Average();
                _minFPS = Mathf.Min(_minFPS, _currentFPS);
                _maxFPS = Mathf.Max(_maxFPS, _currentFPS);

                // 更新历史数据
                lock (_fpsHistory)
                {
                    _fpsHistory.Enqueue(_currentFPS);
                    if (_fpsHistory.Count > MaxFPSHistorySize)
                    {
                        _fpsHistory.Dequeue();
                    }
                }
            }

            // 检测GC发生
            UpdateGCStatistics();

            // 定期记录性能数据快照
            if (UnityEngine.Time.realtimeSinceStartup - _lastSnapshotTime >= SnapshotInterval)
            {
                RecordPerformanceSnapshot();
                _lastSnapshotTime = UnityEngine.Time.realtimeSinceStartup;
            }
        }

        /// <summary>
        /// 更新GC统计信息
        /// </summary>
        private void UpdateGCStatistics()
        {
            // 初始化游戏开始时间（如果还未初始化）
            if (_gameStartTime <= 0f)
            {
                lock (_gcStatsLock)
                {
                    if (_gameStartTime <= 0f)
                    {
                        _gameStartTime =UnityEngine.Time.realtimeSinceStartup;
                        _lastGCCount = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
                    }
                }
            }

            var currentGCCount = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
            if (currentGCCount > _lastGCCount)
            {
                lock (_gcStatsLock)
                {
                    _lastGCCount = currentGCCount;
                    _lastGCTime =UnityEngine.Time.realtimeSinceStartup;
                }
            }
        }

        #endregion

        /// <summary>
        /// 记录性能数据快照
        /// </summary>
        private void RecordPerformanceSnapshot()
        {
            lock (_performanceDataHistoryLock)
            {
                var snapshot = new PerformanceDataSnapshot
                {
                    Timestamp = DateTime.Now,
                    CurrentFPS = _currentFPS,
                    AverageFPS = _averageFPS,
                    MinFPS = _minFPS == float.MaxValue ? 0f : _minFPS,
                    MaxFPS = _maxFPS,
                    Memory = GetMemorySnapshot(),
                    GC = GetGCStatistics(),
                    Resources = GetResourceStatistics()
                };

                _performanceDataHistory.Add(snapshot);

                // 限制历史记录数量
                if (_performanceDataHistory.Count > MaxPerformanceDataHistorySize)
                {
                    _performanceDataHistory.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// 保存性能数据到文件
        /// </summary>
        public async UniTask<bool> SavePerformanceDataAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                var snapshot = new PerformanceDataSnapshot
                {
                    Timestamp = DateTime.Now,
                    CurrentFPS = _currentFPS,
                    AverageFPS = _averageFPS,
                    MinFPS = _minFPS == float.MaxValue ? 0f : _minFPS,
                    MaxFPS = _maxFPS,
                    Memory = GetMemorySnapshot(),
                    GC = GetGCStatistics(),
                    Resources = GetResourceStatistics()
                };

                // 简单的JSON序列化（如果项目中有JSON库可以使用）
                var json = SerializePerformanceSnapshot(snapshot);
                
                // 写入文件
                await System.IO.File.WriteAllTextAsync(filePath, json, cancellationToken);
                
                return true;
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 保存性能数据失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从文件加载性能数据
        /// </summary>
        public async UniTask<PerformanceDataSnapshot> LoadPerformanceDataAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!System.IO.File.Exists(filePath))
                {
                    LogWarning($"[{Name}] 性能数据文件不存在: {filePath}");
                    return null;
                }

                var json = await System.IO.File.ReadAllTextAsync(filePath, cancellationToken);
                var snapshot = DeserializePerformanceSnapshot(json);
                
                return snapshot;
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 加载性能数据失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取性能数据历史记录
        /// </summary>
        public List<PerformanceDataSnapshot> GetPerformanceDataHistory(int maxCount = 100)
        {
            lock (_performanceDataHistoryLock)
            {
                return _performanceDataHistory
                    .OrderByDescending(s => s.Timestamp)
                    .Take(maxCount)
                    .ToList();
            }
        }

        /// <summary>
        /// 清除性能数据历史记录
        /// </summary>
        public void ClearPerformanceDataHistory()
        {
            lock (_performanceDataHistoryLock)
            {
                _performanceDataHistory.Clear();
            }
        }

        /// <summary>
        /// 转义JSON字符串中的特殊字符
        /// </summary>
        private string EscapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            var sb = new System.Text.StringBuilder();
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (c < 0x20)
                        {
                            sb.AppendFormat("\\u{0:X4}", (int)c);
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        /// <summary>
        /// 序列化性能数据快照为JSON（改进版，处理特殊字符）
        /// </summary>
        private string SerializePerformanceSnapshot(PerformanceDataSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return "{}";
            }

            var sb = new System.Text.StringBuilder();
            sb.Append("{");
            sb.Append($"\"timestamp\":\"{snapshot.Timestamp:yyyy-MM-dd HH:mm:ss}\",");
            sb.Append("\"fps\":{");
            sb.Append($"\"current\":{snapshot.CurrentFPS:F2},");
            sb.Append($"\"average\":{snapshot.AverageFPS:F2},");
            sb.Append($"\"min\":{snapshot.MinFPS:F2},");
            sb.Append($"\"max\":{snapshot.MaxFPS:F2}");
            sb.Append("},");
            sb.Append("\"memory\":{");
            sb.Append($"\"totalMB\":{snapshot.Memory?.TotalMemoryMB ?? 0:F2},");
            sb.Append($"\"allocatedMB\":{snapshot.Memory?.AllocatedMemoryMB ?? 0:F2},");
            sb.Append($"\"reservedMB\":{snapshot.Memory?.ReservedMemoryMB ?? 0:F2},");
            sb.Append($"\"monoHeapSizeMB\":{snapshot.Memory?.MonoHeapSizeMB ?? 0:F2},");
            sb.Append($"\"monoUsedSizeMB\":{snapshot.Memory?.MonoUsedSizeMB ?? 0:F2},");
            sb.Append($"\"gcTotalAllocatedMB\":{snapshot.Memory?.GCTotalAllocatedMB ?? 0:F2}");
            sb.Append("},");
            sb.Append("\"gc\":{");
            sb.Append($"\"totalAllocatedMB\":{snapshot.GC?.TotalAllocatedMB ?? 0:F2},");
            sb.Append($"\"monoHeapSizeMB\":{snapshot.GC?.MonoHeapSizeMB ?? 0:F2},");
            sb.Append($"\"monoUsedSizeMB\":{snapshot.GC?.MonoUsedSizeMB ?? 0:F2},");
            sb.Append($"\"gcCount\":{snapshot.GC?.GCCount ?? 0},");
            sb.Append($"\"gcFrequency\":{snapshot.GC?.GCFrequency ?? 0:F4},");
            sb.Append($"\"lastGCTime\":{snapshot.GC?.LastGCTime ?? 0:F2}");
            sb.Append("},");
            sb.Append("\"resources\":{");
            sb.Append($"\"textureCount\":{snapshot.Resources?.TextureCount ?? 0},");
            sb.Append($"\"textureMemoryMB\":{snapshot.Resources?.TextureMemoryMB ?? 0:F2},");
            sb.Append($"\"audioClipCount\":{snapshot.Resources?.AudioClipCount ?? 0},");
            sb.Append($"\"audioClipMemoryMB\":{snapshot.Resources?.AudioClipMemoryMB ?? 0:F2},");
            sb.Append($"\"meshCount\":{snapshot.Resources?.MeshCount ?? 0},");
            sb.Append($"\"meshMemoryMB\":{snapshot.Resources?.MeshMemoryMB ?? 0:F2},");
            sb.Append($"\"materialCount\":{snapshot.Resources?.MaterialCount ?? 0},");
            sb.Append($"\"gameObjectCount\":{snapshot.Resources?.GameObjectCount ?? 0}");
            sb.Append("}");
            sb.Append("}");
            return sb.ToString();
        }

        /// <summary>
        /// 从JSON反序列化性能数据快照（简化版，使用Unity JsonUtility需要可序列化类）
        /// 注意：这是一个简化实现，完整实现需要使用JSON库或创建可序列化包装类
        /// </summary>
        private PerformanceDataSnapshot DeserializePerformanceSnapshot(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                // 简化实现：由于PerformanceDataSnapshot包含嵌套对象，使用Unity JsonUtility需要创建包装类
                // 这里返回一个基本的快照，实际项目中建议：
                // 1. 使用LitJson等库进行完整解析
                // 2. 或者创建可序列化的包装类使用JsonUtility
                // 3. 或者使用System.Text.Json（.NET Core）
                
                // 尝试使用Unity JsonUtility（需要创建包装类）
                // 这里暂时返回一个基本快照，实际数据需要从JSON中解析
                return new PerformanceDataSnapshot
                {
                    Timestamp = DateTime.Now,
                    Memory = new MemorySnapshot(),
                    GC = new GCStatistics(),
                    Resources = new ResourceStatistics()
                };
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 反序列化性能数据失败: {ex.Message}");
                return null;
            }
        }

        protected override UniTask OnShutdownAsync()
        {
            ClearSamples();
            lock (_fpsHistory)
            {
                _fpsHistory.Clear();
            }
            _fpsSamples.Clear();
            lock (_performanceDataHistoryLock)
            {
                _performanceDataHistory.Clear();
            }
            return base.OnShutdownAsync();
        }
    }
}

