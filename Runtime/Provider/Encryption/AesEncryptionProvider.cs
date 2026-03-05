using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Provider.Base;

namespace JulyCore.Provider.Encryption
{
    /// <summary>
    /// AES加密提供者
    /// 使用AES-256-CBC模式进行加密
    /// </summary>
    internal class AesEncryptionProvider : ProviderBase, IEncryptionProvider
    {
        public override int Priority => Frameworkconst.PriorityEncryptionProvider;
        protected override LogChannel LogChannel => LogChannel.Encryption;

        private byte[] _encryptionKey;
        private byte[] _encryptionIV;

        protected override UniTask OnInitAsync()
        {
            try
            {
                InitializeEncryption();
                Log($"[{Name}] AES加密提供者初始化完成");
                return UniTask.CompletedTask;
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 初始化失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 初始化加密密钥
        /// </summary>
        private void InitializeEncryption()
        {
            // TODO: 支持通过配置设置加密密钥
            // 当前使用默认密钥（仅开发环境，生产环境请重写此方法或通过其他方式注入密钥）
            var configKey = "JulyGF_Default_Encryption_Key_32Bytes!!";
            LogWarning($"[{Name}] 使用默认加密密钥，生产环境请配置自定义密钥");

            // 将字符串密钥转换为字节数组
            var keyBytes = Encoding.UTF8.GetBytes(configKey);
            
            // 使用SHA256确保密钥长度为32字节（AES-256需要）
            using (var sha256 = SHA256.Create())
            {
                _encryptionKey = sha256.ComputeHash(keyBytes);
            }

            // 初始化向量（16字节，使用密钥的前16字节）
            _encryptionIV = new byte[16];
            Array.Copy(_encryptionKey, 0, _encryptionIV, 0, 16);
        }

        public byte[] Encrypt(byte[] data)
        {
            try
            {
                if (data == null || data.Length == 0)
                {
                    LogWarning($"[{Name}] 尝试加密空数据");
                    return Array.Empty<byte>();
                }

                using (var aes = Aes.Create())
                {
                    aes.Key = _encryptionKey;
                    aes.IV = _encryptionIV;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using (var encryptor = aes.CreateEncryptor())
                    using (var ms = new MemoryStream())
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        cs.Write(data, 0, data.Length);
                        cs.FlushFinalBlock();
                        return ms.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 加密异常: {ex.Message}");
                return null;
            }
        }

        public byte[] Decrypt(byte[] encryptedData)
        {
            try
            {
                if (encryptedData == null || encryptedData.Length == 0)
                {
                    LogWarning($"[{Name}] 尝试解密空数据");
                    return Array.Empty<byte>();
                }

                using (var aes = Aes.Create())
                {
                    aes.Key = _encryptionKey;
                    aes.IV = _encryptionIV;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using (var decryptor = aes.CreateDecryptor())
                    using (var ms = new MemoryStream(encryptedData))
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (var result = new MemoryStream())
                    {
                        cs.CopyTo(result);
                        return result.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 解密异常: {ex.Message}");
                return null;
            }
        }
    }
}

