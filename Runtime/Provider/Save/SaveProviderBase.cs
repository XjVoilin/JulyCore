using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Data.Save;
using JulyCore.Provider.Base;
using JulyCore.Provider.Data;
using JulyCore.Provider.Encryption;

namespace JulyCore.Provider.Save
{
    /// <summary>
    /// 存档提供者基类
    /// </summary>
    internal abstract class SaveProviderBase : ProviderBase, ISaveProvider
    {
        public override int Priority => Frameworkconst.PrioritySaveProvider;
        protected override LogChannel LogChannel => LogChannel.Save;

        protected readonly ISerializeProvider _serializeProvider;
        protected readonly IEncryptionProvider _encryptionProvider;

        /// <summary>
        /// 已注册的存档数据
        /// </summary>
        private readonly Dictionary<string, ISaveData> _registeredData = new();

        /// <summary>
        /// 脏数据标记
        /// </summary>
        private readonly HashSet<string> _dirtyKeys = new();

        /// <summary>
        /// 保护锁对象
        /// </summary>
        private readonly object _lock = new();

        /// <summary>
        /// 存档格式版本号（当前版本）
        /// </summary>
        protected const byte CurrentSaveVersion = 1;

        /// <summary>
        /// 支持的最低版本号
        /// </summary>
        protected const byte MinimumSupportedVersion = 1;

        /// <summary>
        /// 最大重试次数
        /// </summary>
        protected const int MaxRetryCount = 3;

        /// <summary>
        /// 重试延迟（毫秒）
        /// </summary>
        protected const int RetryDelayMs = 100;

        /// <summary>
        /// 构造函数（依赖通过 DI 容器注入）
        /// </summary>
        protected SaveProviderBase(ISerializeProvider serializeProvider, IEncryptionProvider encryptionProvider)
        {
            _serializeProvider = serializeProvider ?? throw new ArgumentNullException(nameof(serializeProvider));
            _encryptionProvider = encryptionProvider ?? throw new ArgumentNullException(nameof(encryptionProvider));
        }

        /// <summary>
        /// 处理保存前的数据（序列化 + 加密）
        /// </summary>
        private (byte[] data, SaveFailureReason? failureReason) ProcessBeforeSave<T>(T data, string key) where T : ISaveData
        {
            if (data == null)
            {
                return (null, SaveFailureReason.InvalidData);
            }

            // 序列化
            byte[] bytes;
            try
            {
                bytes = _serializeProvider.Serialize(data);
                if (bytes == null || bytes.Length == 0)
                {
                    LogWarning($"[{Name}] 序列化数据为空: {key}");
                    return (null, SaveFailureReason.SerializationFailed);
                }
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 序列化失败: {key}, 错误: {ex.Message}");
                return (null, SaveFailureReason.SerializationFailed);
            }

            // 加密
            byte[] encryptedBytes;
            try
            {
                encryptedBytes = _encryptionProvider.Encrypt(bytes);
                if (encryptedBytes == null || encryptedBytes.Length == 0)
                {
                    LogError($"[{Name}] 加密失败: {key}");
                    return (null, SaveFailureReason.EncryptionFailed);
                }
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 加密失败: {key}, 错误: {ex.Message}");
                return (null, SaveFailureReason.EncryptionFailed);
            }

            return (CreateSaveData(encryptedBytes), null);
        }

        /// <summary>
        /// 处理加载后的数据（解密 + 反序列化）
        /// </summary>
        protected T ProcessAfterLoad<T>(byte[] rawBytes, string key) where T : ISaveData
        {
            if (rawBytes == null || rawBytes.Length == 0)
            {
                return default(T);
            }

            var bytes = ParseSaveData(rawBytes, key);
            if (bytes == null || bytes.Length == 0)
            {
                return default(T);
            }

            var decryptedBytes = _encryptionProvider.Decrypt(bytes);
            if (decryptedBytes == null || decryptedBytes.Length == 0)
            {
                LogError($"[{Name}] 解密失败: {key}");
                return default(T);
            }

            return _serializeProvider.Deserialize<T>(decryptedBytes);
        }

        /// <summary>
        /// 创建带版本标识的存档数据
        /// </summary>
        private byte[] CreateSaveData(byte[] encryptedData)
        {
            const int headerSize = 5;

            var result = new byte[headerSize + encryptedData.Length];
            var offset = 0;

            result[offset++] = CurrentSaveVersion;

            var lengthBytes = BitConverter.GetBytes(encryptedData.Length);
            Array.Copy(lengthBytes, 0, result, offset, 4);
            offset += 4;

            Array.Copy(encryptedData, 0, result, offset, encryptedData.Length);

            return result;
        }

        /// <summary>
        /// 解析带版本标识的存档数据
        /// </summary>
        private byte[] ParseSaveData(byte[] rawData, string key)
        {
            if (rawData.Length < 5)
            {
                LogError($"[{Name}] 存档数据格式无效（长度不足）: {key}");
                return null;
            }

            var offset = 0;
            var version = rawData[offset++];

            if (version < MinimumSupportedVersion)
            {
                LogError($"[{Name}] 存档版本过低: {version} (最低: {MinimumSupportedVersion}), key: {key}");
                return null;
            }

            if (version > CurrentSaveVersion)
            {
                LogError($"[{Name}] 存档版本过高: {version} (当前: {CurrentSaveVersion}), key: {key}");
                return null;
            }

            if (MinimumSupportedVersion != version)
            {
                LogWarning($"[{Name}] 检测到旧版本存档: {version}，尝试迁移, key: {key}");
                var migratedData = MigrateSaveData(rawData, version, key);
                if (migratedData == null)
                {
                    LogError($"[{Name}] 存档迁移失败: {key}");
                    return null;
                }
                rawData = migratedData;
                offset = 1;
            }

            var dataLength = BitConverter.ToInt32(rawData, offset);
            offset += 4;

            if (dataLength < 0 || offset + dataLength > rawData.Length)
            {
                LogError($"[{Name}] 存档数据长度无效: {dataLength}, key: {key}");
                return null;
            }

            var data = new byte[dataLength];
            Array.Copy(rawData, offset, data, 0, dataLength);

            return data;
        }

        /// <summary>
        /// 迁移存档数据到当前版本
        /// </summary>
        protected virtual byte[] MigrateSaveData(byte[] rawData, byte fromVersion, string key)
        {
            LogWarning($"[{Name}] 未实现从版本 {fromVersion} 到 {CurrentSaveVersion} 的迁移: {key}");
            return null;
        }

        /// <summary>
        /// 分类异常并返回失败原因
        /// </summary>
        private SaveFailureReason ClassifyException(Exception ex)
        {
            if (ex is OperationCanceledException)
            {
                return SaveFailureReason.Cancelled;
            }

            if (ex is UnauthorizedAccessException || ex is System.Security.SecurityException)
            {
                return SaveFailureReason.PermissionDenied;
            }

            if (ex is IOException ioEx)
            {
                var errorCode = System.Runtime.InteropServices.Marshal.GetHRForException(ioEx);
                if (errorCode == unchecked((int)0x80070070) || errorCode == unchecked((int)0x80070027))
                {
                    return SaveFailureReason.DiskFull;
                }

                if (errorCode == unchecked((int)0x80070020))
                {
                    return SaveFailureReason.FileInUse;
                }
            }

            if (ex is IOException || ex is SystemException)
            {
                return SaveFailureReason.DeviceError;
            }

            return SaveFailureReason.Unknown;
        }

        /// <summary>
        /// 带重试的保存操作
        /// </summary>
        protected async UniTask<SaveResult> SaveWithRetryAsync<T>(
            string key,
            T data,
            Func<string, byte[], CancellationToken, UniTask<bool>> saveAction,
            CancellationToken cancellationToken = default) where T : ISaveData
        {
            var (processedData, failureReason) = ProcessBeforeSave(data, key);
            if (processedData == null)
            {
                return SaveResult.CreateFailure(failureReason ?? SaveFailureReason.Unknown);
            }

            var backupPath = await BackupSaveDataAsync(key, cancellationToken);

            Exception lastException = null;
            for (int attempt = 0; attempt < MaxRetryCount; attempt++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    await RestoreBackupAsync(key, backupPath, cancellationToken);
                    return SaveResult.CreateFailure(SaveFailureReason.Cancelled);
                }

                try
                {
                    var success = await saveAction(key, processedData, cancellationToken);
                    if (success)
                    {
                        DeleteBackupAsync(backupPath);
                        return SaveResult.CreateSuccess();
                    }
                }
                catch (OperationCanceledException)
                {
                    await RestoreBackupAsync(key, backupPath, cancellationToken);
                    return SaveResult.CreateFailure(SaveFailureReason.Cancelled);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    LogWarning($"[{Name}] 保存失败 (尝试 {attempt + 1}/{MaxRetryCount}): {key}, 错误: {ex.Message}");
                }

                if (attempt < MaxRetryCount - 1)
                {
                    await UniTask.Delay(RetryDelayMs * (attempt + 1), cancellationToken: cancellationToken);
                }
            }

            await RestoreBackupAsync(key, backupPath, cancellationToken);

            var reason = lastException != null ? ClassifyException(lastException) : SaveFailureReason.Unknown;
            return SaveResult.CreateFailure(reason, lastException?.Message);
        }

        /// <summary>
        /// 备份存档数据（子类实现）
        /// </summary>
        protected virtual UniTask<string> BackupSaveDataAsync(string key, CancellationToken cancellationToken)
        {
            return UniTask.FromResult<string>(null);
        }

        /// <summary>
        /// 恢复备份数据（子类实现）
        /// </summary>
        protected virtual UniTask RestoreBackupAsync(string key, string backupPath, CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 删除备份数据（子类实现）
        /// </summary>
        protected virtual void DeleteBackupAsync(string backupPath)
        {
        }

        /// <summary>
        /// 批量保存的默认实现
        /// </summary>
        public virtual async UniTask<Dictionary<string, SaveResult>> SaveBatchAsync<T>(
            Dictionary<string, T> dataMap,
            CancellationToken cancellationToken = default) where T : ISaveData
        {
            var results = new Dictionary<string, SaveResult>(dataMap.Count);
            foreach (var kvp in dataMap)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    results[kvp.Key] = SaveResult.CreateFailure(SaveFailureReason.Cancelled);
                    continue;
                }

                results[kvp.Key] = await SaveAsync(kvp.Key, kvp.Value, cancellationToken);
            }
            return results;
        }

        /// <summary>
        /// 批量加载的默认实现
        /// </summary>
        public virtual async UniTask<Dictionary<string, T>> LoadBatchAsync<T>(
            string[] keys,
            CancellationToken cancellationToken = default) where T : ISaveData
        {
            var results = new Dictionary<string, T>(keys.Length);
            foreach (var key in keys)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var data = await LoadAsync<T>(key, cancellationToken);
                if (data != null)
                {
                    results[key] = data;
                }
            }
            return results;
        }

        #region 数据注册管理

        /// <summary>
        /// 注册存档数据
        /// </summary>
        public void Register(string key, ISaveData data)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key), "存档键不能为空");
            }
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data), "存档数据不能为空");
            }

            lock (_lock)
            {
                if (_registeredData.ContainsKey(key))
                {
                    LogWarning($"[{Name}] 存档数据已注册，将覆盖: {key}");
                }
                _registeredData[key] = data;
            }
        }

        /// <summary>
        /// 注销存档数据
        /// </summary>
        public bool Unregister(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            lock (_lock)
            {
                _dirtyKeys.Remove(key);
                return _registeredData.Remove(key);
            }
        }

        /// <summary>
        /// 检查数据是否已注册
        /// </summary>
        public bool IsRegistered(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            lock (_lock)
            {
                return _registeredData.ContainsKey(key);
            }
        }

        /// <summary>
        /// 获取已注册的存档数据
        /// </summary>
        public T GetRegisteredData<T>(string key) where T : class, ISaveData
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            lock (_lock)
            {
                if (_registeredData.TryGetValue(key, out var data))
                {
                    return data as T;
                }
                return null;
            }
        }

        /// <summary>
        /// 获取所有已注册的存档键
        /// </summary>
        public IEnumerable<string> GetAllRegisteredKeys()
        {
            lock (_lock)
            {
                return _registeredData.Keys.ToList();
            }
        }

        #endregion

        #region 脏标记管理

        /// <summary>
        /// 标记数据为脏
        /// </summary>
        public bool MarkDirty(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            lock (_lock)
            {
                if (!_registeredData.ContainsKey(key))
                {
                    LogWarning($"[{Name}] 无法标记脏数据，数据未注册: {key}");
                    return false;
                }

                _dirtyKeys.Add(key);
                return true;
            }
        }

        /// <summary>
        /// 检查数据是否为脏
        /// </summary>
        public bool IsDirty(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            lock (_lock)
            {
                return _dirtyKeys.Contains(key);
            }
        }

        /// <summary>
        /// 获取所有脏数据的键
        /// </summary>
        public IEnumerable<string> GetDirtyKeys()
        {
            lock (_lock)
            {
                return _dirtyKeys.ToList();
            }
        }

        /// <summary>
        /// 获取当前脏数据数量
        /// </summary>
        public int DirtyCount
        {
            get
            {
                lock (_lock)
                {
                    return _dirtyKeys.Count;
                }
            }
        }

        /// <summary>
        /// 清除指定数据的脏标记
        /// </summary>
        public void ClearDirty(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            lock (_lock)
            {
                _dirtyKeys.Remove(key);
            }
        }

        /// <summary>
        /// 清除所有脏标记
        /// </summary>
        public void ClearAllDirty()
        {
            lock (_lock)
            {
                _dirtyKeys.Clear();
            }
        }

        #endregion

        #region 加载与保存

        /// <summary>
        /// 加载数据并自动注册
        /// </summary>
        public async UniTask<T> LoadAndRegisterAsync<T>(string key, CancellationToken cancellationToken = default) 
            where T : ISaveData, new()
        {
            T data;
            if (HasSave(key))
            {
                data = await LoadAsync<T>(key, cancellationToken);
                if (data == null)
                {
                    data = new T();
                }
            }
            else
            {
                data = new T();
            }

            Register(key, data);
            return data;
        }

        /// <summary>
        /// 批量加载数据并自动注册
        /// </summary>
        public async UniTask<Dictionary<string, T>> LoadAndRegisterBatchAsync<T>(string[] keys, CancellationToken cancellationToken = default)
            where T : ISaveData, new()
        {
            var results = new Dictionary<string, T>(keys.Length);
            foreach (var key in keys)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var data = await LoadAndRegisterAsync<T>(key, cancellationToken);
                results[key] = data;
            }
            return results;
        }

        /// <summary>
        /// 批量保存已注册的脏数据
        /// </summary>
        public async UniTask<Dictionary<string, SaveResult>> SaveRegisteredAsync(
            IEnumerable<string> keys = null,
            CancellationToken cancellationToken = default)
        {
            var results = new Dictionary<string, SaveResult>();
            List<string> keysToSave;

            lock (_lock)
            {
                if (keys == null)
                {
                    // 保存所有脏数据
                    keysToSave = _dirtyKeys.ToList();
                }
                else
                {
                    // 只保存指定的键（且必须是脏的）
                    keysToSave = keys.Where(k => _dirtyKeys.Contains(k)).ToList();
                }
            }

            if (keysToSave.Count == 0)
            {
                return results;
            }

            foreach (var key in keysToSave)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    results[key] = SaveResult.CreateFailure(SaveFailureReason.Cancelled);
                    continue;
                }

                ISaveData data;
                lock (_lock)
                {
                    if (!_registeredData.TryGetValue(key, out data))
                    {
                        continue;
                    }
                }

                var result = await SaveAsync(key, data, cancellationToken);
                results[key] = result;

                if (result.Success)
                {
                    ClearDirty(key);
                }
            }

            return results;
        }

        /// <summary>
        /// 删除存档（同时注销数据）
        /// </summary>
        public bool Delete(string key)
        {
            Unregister(key);
            return DeleteInternal(key);
        }

        #endregion

        /// <summary>
        /// 关闭时清理
        /// </summary>
        protected override UniTask OnShutdownAsync()
        {
            lock (_lock)
            {
                _registeredData.Clear();
                _dirtyKeys.Clear();
            }
            return base.OnShutdownAsync();
        }

        // 抽象方法，子类必须实现
        public abstract UniTask<SaveResult> SaveAsync<T>(string key, T data, CancellationToken cancellationToken = default) where T : ISaveData;
        public abstract UniTask<T> LoadAsync<T>(string key, CancellationToken cancellationToken = default) where T : ISaveData;
        protected abstract bool DeleteInternal(string key);
        public abstract bool HasSave(string key);
        public abstract string GetSavePath(string key);
    }
}
