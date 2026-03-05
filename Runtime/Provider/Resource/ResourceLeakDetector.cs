using System;
using System.Collections.Generic;
using JulyCore.Core;
using UnityEngine;

namespace JulyCore.Provider.Resource
{
    /// <summary>
    /// 资源泄漏检测配置
    /// </summary>
    [Serializable]
    public class ResourceLeakDetectorConfig
    {
        /// <summary>
        /// 是否启用泄漏检测
        /// </summary>
        [Tooltip("是否启用泄漏检测")]
        public bool EnableLeakDetection = true;

        /// <summary>
        /// 泄漏检测间隔（秒）
        /// </summary>
        [Tooltip("泄漏检测间隔（秒）")]
        [Range(10f, 300f)]
        public float DetectionInterval = 60f;

        /// <summary>
        /// 资源存活时间阈值（秒），超过此时间的未释放句柄视为可疑
        /// </summary>
        [Tooltip("资源存活时间阈值（秒）")]
        [Range(30f, 600f)]
        public float SuspiciousAliveTimeSeconds = 120f;

        /// <summary>
        /// 引用计数阈值，超过此值视为可能泄漏
        /// </summary>
        [Tooltip("引用计数阈值")]
        [Range(5, 100)]
        public int RefCountThreshold = 10;

        /// <summary>
        /// 每帧处理释放队列的最大数量
        /// </summary>
        [Tooltip("每帧处理释放队列的最大数量")]
        [Range(1, 50)]
        public int MaxReleasePerFrame = 10;

        /// <summary>
        /// 是否自动清理长时间未释放的资源
        /// </summary>
        [Tooltip("是否自动清理长时间未释放的资源")]
        public bool AutoCleanupEnabled = false;

        /// <summary>
        /// 自动清理的存活时间阈值（秒）
        /// </summary>
        [Tooltip("自动清理的存活时间阈值（秒）")]
        [Range(300f, 3600f)]
        public float AutoCleanupThresholdSeconds = 600f;
    }

    /// <summary>
    /// 资源泄漏检测器
    /// 
    /// 【功能】
    /// 1. 处理资源释放队列（主线程安全释放）
    /// 2. 定期检测资源泄漏
    /// 3. 可选的自动清理长时间未释放的资源
    /// 4. 生成泄漏报告
    /// 
    /// 【使用方式】
    /// 该组件会自动在框架初始化时创建，挂载到 FrameworkRoot 对象上
    /// 也可以手动创建：ResourceLeakDetector.CreateInstance()
    /// </summary>
    public class ResourceLeakDetector : MonoBehaviour
    {
        private static ResourceLeakDetector _instance;

        /// <summary>
        /// 单例实例
        /// </summary>
        public static ResourceLeakDetector Instance => _instance;

        /// <summary>
        /// 检测配置
        /// </summary>
        [SerializeField]
        private ResourceLeakDetectorConfig _config = new ResourceLeakDetectorConfig();

        /// <summary>
        /// 配置访问器
        /// </summary>
        public ResourceLeakDetectorConfig Config => _config;

        private IResourceProvider _resourceProvider;
        private float _lastDetectionTime;
        private readonly List<ResourceLeakReport> _leakHistory = new List<ResourceLeakReport>();
        private const int MaxHistorySize = 100;

        /// <summary>
        /// 泄漏检测事件
        /// </summary>
        public event Action<ResourceLeakReport> OnLeakDetected;

        /// <summary>
        /// 创建检测器实例
        /// </summary>
        public static ResourceLeakDetector CreateInstance(IResourceProvider resourceProvider = null)
        {
            if (_instance != null)
            {
                return _instance;
            }

            var go = new GameObject("[ResourceLeakDetector]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ResourceLeakDetector>();
            _instance._resourceProvider = resourceProvider;
            
            JLogger.Log("[ResourceLeakDetector] 资源泄漏检测器已创建");
            return _instance;
        }

        /// <summary>
        /// 设置资源提供者
        /// </summary>
        public void SetResourceProvider(IResourceProvider provider)
        {
            _resourceProvider = provider;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void Update()
        {
            // 处理释放队列
            ProcessReleaseQueue();

            // 定期泄漏检测
            if (_config.EnableLeakDetection)
            {
                PerformPeriodicDetection();
            }
        }

        /// <summary>
        /// 处理释放队列
        /// </summary>
        private void ProcessReleaseQueue()
        {
            var processed = ResourceReleaseQueue.ProcessReleaseQueue(_config.MaxReleasePerFrame);
            if (processed > 0)
            {
                JLogger.Log($"[ResourceLeakDetector] 处理了 {processed} 个延迟释放的资源");
            }
        }

        /// <summary>
        /// 定期执行泄漏检测
        /// </summary>
        private void PerformPeriodicDetection()
        {
            if (UnityEngine.Time.realtimeSinceStartup - _lastDetectionTime < _config.DetectionInterval)
            {
                return;
            }

            _lastDetectionTime = UnityEngine.Time.realtimeSinceStartup;
            PerformLeakDetection();
        }

        /// <summary>
        /// 执行泄漏检测
        /// </summary>
        public ResourceLeakReport PerformLeakDetection()
        {
            if (_resourceProvider == null)
            {
                return null;
            }

            var report = new ResourceLeakReport
            {
                DetectionTime = DateTime.Now,
                SuspiciousHandles = new List<ActiveHandleInfo>(),
                LeakedResources = new List<ResourceLeakInfo>(),
                Statistics = _resourceProvider.GetStatistics()
            };

            // 检测长时间存活的句柄
            var activeHandles = _resourceProvider.GetActiveHandles();
            foreach (var handle in activeHandles)
            {
                if (handle.AliveSeconds > _config.SuspiciousAliveTimeSeconds)
                {
                    report.SuspiciousHandles.Add(handle);
                }
            }

            // 检测高引用计数的资源
            var leaks = _resourceProvider.DetectLeaks(_config.RefCountThreshold);
            report.LeakedResources.AddRange(leaks);

            // 如果检测到泄漏，记录日志并触发事件
            if (report.HasIssues)
            {
                LogLeakReport(report);
                OnLeakDetected?.Invoke(report);

                // 保存到历史
                _leakHistory.Add(report);
                while (_leakHistory.Count > MaxHistorySize)
                {
                    _leakHistory.RemoveAt(0);
                }

                // 自动清理（如果启用）
                if (_config.AutoCleanupEnabled)
                {
                    PerformAutoCleanup(report);
                }
            }

            return report;
        }

        /// <summary>
        /// 执行自动清理
        /// </summary>
        private void PerformAutoCleanup(ResourceLeakReport report)
        {
            int cleanedCount = 0;

            // 清理超过阈值时间的句柄
            foreach (var handle in report.SuspiciousHandles)
            {
                if (handle.AliveSeconds > _config.AutoCleanupThresholdSeconds)
                {
                    JLogger.LogWarning($"[ResourceLeakDetector] 自动清理长时间未释放的资源: {handle.Path} (存活: {handle.AliveSeconds:F0}秒)");
                    cleanedCount++;
                }
            }

            if (cleanedCount > 0)
            {
                JLogger.LogWarning($"[ResourceLeakDetector] 自动清理了 {cleanedCount} 个资源（建议检查代码逻辑）");
            }
        }

        /// <summary>
        /// 记录泄漏报告日志
        /// </summary>
        private void LogLeakReport(ResourceLeakReport report)
        {
            var message = $"[ResourceLeakDetector] 检测到资源问题！\n" +
                          $"  可疑句柄数: {report.SuspiciousHandles.Count}\n" +
                          $"  高引用计数资源数: {report.LeakedResources.Count}\n" +
                          $"  统计: 总资源={report.Statistics.TotalResources}, 总引用={report.Statistics.TotalRefCount}";

            if (report.SuspiciousHandles.Count > 0)
            {
                message += "\n\n--- 长时间存活的句柄 ---";
                foreach (var handle in report.SuspiciousHandles)
                {
                    message += $"\n  [{handle.ResourceType}] {handle.Path}";
                    message += $"\n    存活: {handle.AliveSeconds:F0}秒, 绑定: {(handle.IsBoundToObject ? handle.BoundObjectName : "无")}";
                    if (!string.IsNullOrEmpty(handle.LoadStackTrace))
                    {
                        message += $"\n    加载位置:\n{handle.LoadStackTrace}";
                    }
                }
            }

            if (report.LeakedResources.Count > 0)
            {
                message += "\n\n--- 高引用计数资源 ---";
                foreach (var leak in report.LeakedResources)
                {
                    message += $"\n  [{leak.ResourceType}] {leak.Location} (RefCount: {leak.RefCount})";
                }
            }

            JLogger.LogWarning(message);
        }

        /// <summary>
        /// 获取泄漏历史记录
        /// </summary>
        public List<ResourceLeakReport> GetLeakHistory()
        {
            return new List<ResourceLeakReport>(_leakHistory);
        }

        /// <summary>
        /// 清空泄漏历史
        /// </summary>
        public void ClearLeakHistory()
        {
            _leakHistory.Clear();
        }

        /// <summary>
        /// 强制触发一次垃圾回收和泄漏检测
        /// </summary>
        public ResourceLeakReport ForceCollectAndDetect()
        {
            // 触发 GC，让析构函数执行，将遗漏的资源加入释放队列
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // 处理释放队列
            while (ResourceReleaseQueue.PendingCount > 0)
            {
                ResourceReleaseQueue.ProcessReleaseQueue(100);
            }

            // 执行泄漏检测
            return PerformLeakDetection();
        }

        private void OnDestroy()
        {
            // 清理释放队列
            while (ResourceReleaseQueue.PendingCount > 0)
            {
                ResourceReleaseQueue.ProcessReleaseQueue(100);
            }
            ResourceReleaseQueue.Clear();

            if (_instance == this)
            {
                _instance = null;
            }
        }
    }

    /// <summary>
    /// 资源泄漏报告
    /// </summary>
    public class ResourceLeakReport
    {
        /// <summary>
        /// 检测时间
        /// </summary>
        public DateTime DetectionTime { get; set; }

        /// <summary>
        /// 可疑的长时间存活句柄
        /// </summary>
        public List<ActiveHandleInfo> SuspiciousHandles { get; set; }

        /// <summary>
        /// 高引用计数的资源（可能泄漏）
        /// </summary>
        public List<ResourceLeakInfo> LeakedResources { get; set; }

        /// <summary>
        /// 资源统计
        /// </summary>
        public ResourceStatistics Statistics { get; set; }

        /// <summary>
        /// 是否有问题
        /// </summary>
        public bool HasIssues => (SuspiciousHandles?.Count ?? 0) > 0 || (LeakedResources?.Count ?? 0) > 0;

        /// <summary>
        /// 生成简要报告
        /// </summary>
        public string ToSummary()
        {
            return $"[{DetectionTime:HH:mm:ss}] 可疑句柄: {SuspiciousHandles?.Count ?? 0}, 高引用资源: {LeakedResources?.Count ?? 0}";
        }
    }
}

