namespace JulyCore.Provider.Encryption
{
    /// <summary>
    /// 加密提供者接口
    /// 提供数据加密和解密能力
    /// </summary>
    public interface IEncryptionProvider : Core.IProvider
    {
        /// <summary>
        /// 加密数据
        /// </summary>
        /// <param name="data">原始数据</param>
        /// <returns>加密后的数据，失败返回null</returns>
        byte[] Encrypt(byte[] data);

        /// <summary>
        /// 解密数据
        /// </summary>
        /// <param name="encryptedData">加密的数据</param>
        /// <returns>解密后的数据，失败返回null</returns>
        byte[] Decrypt(byte[] encryptedData);
    }
}

