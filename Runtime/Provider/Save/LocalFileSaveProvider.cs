using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Data.Save;
using JulyCore.Provider.Data;
using JulyCore.Provider.Encryption;
using UnityEngine;

namespace JulyCore.Provider.Save
{
    /// <summary>
    /// 本地文件存档提供者
    /// 负责文件I/O操作，支持数据备份和错误恢复
    /// </summary>
    internal class LocalFileSaveProvider : SaveProviderBase
    {
        private string _saveRootPath;
        private string _backupRootPath;

        /// <summary>
        /// 构造函数（依赖通过 DI 容器注入）
        /// </summary>
        public LocalFileSaveProvider(ISerializeProvider serializeProvider, IEncryptionProvider encryptionProvider)
            : base(serializeProvider, encryptionProvider)
        {
        }

        protected override UniTask OnInitAsync()
        {
            // 获取存档根路径（使用Unity的持久化路径）
            _saveRootPath = Path.Combine(Application.persistentDataPath, "Save");
            _backupRootPath = Path.Combine(Application.persistentDataPath, "Save", "Backup");

            // 确保目录存在
            if (!Directory.Exists(_saveRootPath))
            {
                Directory.CreateDirectory(_saveRootPath);
            }

            if (!Directory.Exists(_backupRootPath))
            {
                Directory.CreateDirectory(_backupRootPath);
            }

            return UniTask.CompletedTask;
        }

        public override async UniTask<SaveResult> SaveAsync<T>(string key, T data,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
            {
                return SaveResult.CreateFailure(SaveFailureReason.InvalidData, "存档key不能为空");
            }

            return await SaveWithRetryAsync(key, data, WriteFileAsync, cancellationToken);
        }

        private async UniTask<bool> WriteFileAsync(string key, byte[] data, CancellationToken cancellationToken)
        {
            var filePath = GetSavePath(key);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(filePath, data, cancellationToken);
            return true;
        }

        public override async UniTask<T> LoadAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(key))
                {
                    LogWarning($"[{Name}] 存档key不能为空");
                    return default(T);
                }

                var filePath = GetSavePath(key);
                if (!File.Exists(filePath))
                {
                    Log($"[{Name}] 存档不存在: {key}");
                    return default(T);
                }

                var rawBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
                if (rawBytes == null || rawBytes.Length == 0)
                {
                    LogWarning($"[{Name}] 存档文件为空: {key}");
                    return default(T);
                }

                var data = ProcessAfterLoad<T>(rawBytes, key);

                return data;
            }
            catch (OperationCanceledException)
            {
                LogWarning($"[{Name}] 加载操作已取消: {key}");
                return default(T);
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 加载失败: {key}, 错误: {ex.Message}");
                return default(T);
            }
        }

        protected override bool DeleteInternal(string key)
        {
            try
            {
                if (string.IsNullOrEmpty(key))
                {
                    return false;
                }

                var filePath = GetSavePath(key);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 删除失败: {key}, 错误: {ex.Message}");
                return false;
            }
        }

        public override bool HasSave(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            var filePath = GetSavePath(key);
            return File.Exists(filePath);
        }

        public override string GetSavePath(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("存档key不能为空", nameof(key));
            }

            // 按路径分隔符拆分 key，对每段分别做文件名安全化
            // 例如 "Slot_1/Player" → Save/Slot_1/Player.dat
            var invalidChars = Path.GetInvalidFileNameChars();
            var segments = key.Split('/');
            for (var i = 0; i < segments.Length; i++)
            {
                segments[i] = string.Join("_", segments[i].Split(invalidChars));
            }

            var relativePath = Path.Combine(segments);
            return Path.Combine(_saveRootPath, $"{relativePath}.dat");
        }

        protected override async UniTask<string> BackupSaveDataAsync(string key, CancellationToken cancellationToken)
        {
            try
            {
                var filePath = GetSavePath(key);
                if (!File.Exists(filePath))
                {
                    return null;
                }

                var backupPath = Path.Combine(_backupRootPath, $"{Path.GetFileName(filePath)}.bak");
                var backupDir = Path.GetDirectoryName(backupPath);
                if (!string.IsNullOrEmpty(backupDir) && !Directory.Exists(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                }

                var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
                await File.WriteAllBytesAsync(backupPath, bytes, cancellationToken);
                return backupPath;
            }
            catch (Exception ex)
            {
                LogWarning($"[{Name}] 备份失败: {key}, 错误: {ex.Message}");
                return null;
            }
        }

        protected override async UniTask RestoreBackupAsync(string key, string backupPath,
            CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrEmpty(backupPath) || !File.Exists(backupPath))
                {
                    return;
                }

                var filePath = GetSavePath(key);
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var bytes = await File.ReadAllBytesAsync(backupPath, cancellationToken);
                await File.WriteAllBytesAsync(filePath, bytes, cancellationToken);
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 恢复备份失败: {key}, 错误: {ex.Message}");
            }
        }

        protected override void DeleteBackupAsync(string backupPath)
        {
            try
            {
                if (string.IsNullOrEmpty(backupPath) || !File.Exists(backupPath))
                {
                    return;
                }

                File.Delete(backupPath);
            }
            catch (Exception ex)
            {
                LogWarning($"[{Name}] 删除备份失败: {backupPath}, 错误: {ex.Message}");
            }
        }
    }
}
