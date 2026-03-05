using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace JulyCore.Provider.Data
{
    /// <summary>
    /// 数据提供者接口
    /// 提供数据序列化和反序列化能力
    /// </summary>
    public interface ISerializeProvider : Core.IProvider
    {
        #region 泛型方法（编译期类型）

        /// <summary>
        /// 序列化数据为字节数组
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="data">要序列化的数据</param>
        /// <returns>序列化后的字节数组</returns>
        byte[] Serialize<T>(T data);

        /// <summary>
        /// 反序列化字节数组为数据对象
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="bytes">要反序列化的字节数组</param>
        /// <returns>反序列化后的数据对象</returns>
        T Deserialize<T>(byte[] bytes);

        /// <summary>
        /// 异步序列化数据为字节数组
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="data">要序列化的数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>序列化后的字节数组</returns>
        UniTask<byte[]> SerializeAsync<T>(T data, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步反序列化字节数组为数据对象
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="bytes">要反序列化的字节数组</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>反序列化后的数据对象</returns>
        UniTask<T> DeserializeAsync<T>(byte[] bytes, CancellationToken cancellationToken = default);

        #endregion

        #region 非泛型方法（运行时动态类型）

        /// <summary>
        /// 序列化对象为 JSON 字符串
        /// </summary>
        /// <param name="data">要序列化的对象</param>
        /// <returns>JSON 字符串</returns>
        string SerializeToJson(object data);

        /// <summary>
        /// 从 JSON 字符串反序列化为指定类型的对象
        /// </summary>
        /// <param name="json">JSON 字符串</param>
        /// <param name="type">目标类型</param>
        /// <returns>反序列化后的对象</returns>
        object DeserializeFromJson(string json, Type type);

        #endregion
    }
}
