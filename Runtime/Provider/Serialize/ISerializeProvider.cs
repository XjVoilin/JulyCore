using System;

namespace JulyCore.Provider.Data
{
    /// <summary>
    /// 数据提供者接口
    /// 提供数据序列化和反序列化能力
    /// </summary>
    public interface ISerializeProvider : Core.IProvider
    {
        #region 泛型方法（编译期类型）

        byte[] Serialize<T>(T data);
        T Deserialize<T>(byte[] bytes);

        #endregion

        #region 非泛型方法（运行时动态类型）

        string SerializeToJson(object data);
        object DeserializeFromJson(string json, Type type);

        #endregion
    }
}
