using System;

namespace JulyCore.Data.Save
{
    /// <summary>
    /// 存档失败原因
    /// </summary>
    public enum SaveFailureReason
    {
        /// <summary>
        /// 成功（无失败）
        /// </summary>
        None = 0,

        /// <summary>
        /// 未知错误
        /// </summary>
        Unknown = 1,

        /// <summary>
        /// 磁盘空间不足
        /// </summary>
        DiskFull = 2,

        /// <summary>
        /// 权限不足
        /// </summary>
        PermissionDenied = 3,

        /// <summary>
        /// 文件被占用
        /// </summary>
        FileInUse = 4,

        /// <summary>
        /// 设备异常
        /// </summary>
        DeviceError = 5,

        /// <summary>
        /// 数据序列化失败
        /// </summary>
        SerializationFailed = 6,

        /// <summary>
        /// 数据加密失败
        /// </summary>
        EncryptionFailed = 7,

        /// <summary>
        /// 数据为空或无效
        /// </summary>
        InvalidData = 8,

        /// <summary>
        /// 操作被取消
        /// </summary>
        Cancelled = 9
    }

    /// <summary>
    /// 存档数据重要性等级
    /// </summary>
    public enum SaveImportance
    {
        /// <summary>
        /// 极重要数据：任何保存信号都会触发保存
        /// 特点：数据丢失会造成严重损失，所有保存信号都会包含此类数据
        /// </summary>
        Critical = 0,

        /// <summary>
        /// 重要数据：Medium 及以上信号触发保存
        /// 特点：数据丢失有一定影响，但不是灾难性的
        /// </summary>
        Important = 1,

        /// <summary>
        /// 一般数据：High 及以上信号触发保存
        /// 特点：更新频繁，丢失影响较小
        /// </summary>
        Normal = 2,

        /// <summary>
        /// 琐碎数据：只在 Immediate 信号（兜底）时保存
        /// 特点：丢失影响极小，只在退出时保存
        /// </summary>
        Trivial = 3
    }

    /// <summary>
    /// 保存信号等级
    /// 
    /// 双重用途：
    /// 1. 作为 TriggerSaveAsync 的参数：决定保存哪些 SaveImportance 级别的数据
    /// 2. 作为 MarkDirtyAndSaveAsync 的参数：决定保存触发时机
    ///    - Low: 仅标记脏，等待自动保存
    ///    - Medium: 累积到一定数量后保存
    ///    - High/Immediate: 立即触发保存
    /// </summary>
    public enum SaveSignal
    {
        /// <summary>
        /// 仅 Critical 级别的脏数据
        /// </summary>
        Low = 0,

        /// <summary>
        /// Critical + Important 级别的脏数据
        /// </summary>
        Medium = 1,

        /// <summary>
        /// Critical + Important + Normal 级别的脏数据
        /// </summary>
        High = 2,

        /// <summary>
        /// 所有脏数据，无论重要性级别
        /// </summary>
        Immediate = 3
    }

    /// <summary>
    /// 存档操作结果
    /// </summary>
    public struct SaveResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; private set; }

        /// <summary>
        /// 失败原因
        /// </summary>
        public SaveFailureReason FailureReason { get; private set; }

        /// <summary>
        /// 失败消息（用于提示用户）
        /// </summary>
        public string FailureMessage { get; private set; }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static SaveResult CreateSuccess()
        {
            return new SaveResult
            {
                Success = true,
                FailureReason = SaveFailureReason.None,
                FailureMessage = string.Empty
            };
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static SaveResult CreateFailure(SaveFailureReason reason, string message = null)
        {
            return new SaveResult
            {
                Success = false,
                FailureReason = reason,
                FailureMessage = message ?? GetDefaultFailureMessage(reason)
            };
        }

        /// <summary>
        /// 获取默认失败消息
        /// </summary>
        private static string GetDefaultFailureMessage(SaveFailureReason reason)
        {
            return reason switch
            {
                SaveFailureReason.DiskFull => "磁盘空间不足，无法保存游戏数据",
                SaveFailureReason.PermissionDenied => "没有写入权限，无法保存游戏数据",
                SaveFailureReason.FileInUse => "存档文件被占用，请稍后重试",
                SaveFailureReason.DeviceError => "设备异常，无法保存游戏数据",
                SaveFailureReason.SerializationFailed => "数据序列化失败，无法保存",
                SaveFailureReason.EncryptionFailed => "数据加密失败，无法保存",
                SaveFailureReason.InvalidData => "数据无效，无法保存",
                SaveFailureReason.Cancelled => "保存操作已取消",
                _ => "保存失败，请稍后重试"
            };
        }
    }

    /// <summary>
    /// 保存上下文
    /// </summary>
    public readonly struct SaveContext
    {
        /// <summary>
        /// 保存信号级别
        /// </summary>
        public SaveSignal Signal { get; }

        /// <summary>
        /// 存档键
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// 存档数据
        /// </summary>
        public ISaveData Data { get; }

        /// <summary>
        /// 创建保存上下文
        /// </summary>
        /// <param name="signal">保存信号级别</param>
        /// <param name="key">存档键</param>
        /// <param name="data">存档数据</param>
        public SaveContext(SaveSignal signal, string key, ISaveData data)
        {
            Signal = signal;
            Key = key;
            Data = data;
        }
    }

    /// <summary>
    /// 存档数据接口
    /// </summary>
    public interface ISaveData
    {
        /// <summary>
        /// 数据重要性等级
        /// </summary>
        SaveImportance Importance { get; }
    }
}

