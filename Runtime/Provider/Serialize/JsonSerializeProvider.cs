using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Provider.Base;
using LitJson;

namespace JulyCore.Provider.Data
{
    /// <summary>
    /// JSON数据提供者
    /// 使用 LitJson 进行序列化和反序列化
    /// </summary>
    internal class JsonSerializeProvider : ProviderBase, ISerializeProvider
    {
        public override int Priority => Frameworkconst.PrioritySerializeProvider;
        protected override LogChannel LogChannel => LogChannel.Serialize;

        #region 泛型方法

        /// <summary>
        /// 序列化数据为字节数组
        /// </summary>
        public byte[] Serialize<T>(T data)
        {
            try
            {
                if (data == null)
                {
                    LogWarning($"[{Name}] 尝试序列化空数据");
                    return Array.Empty<byte>();
                }

                var json = JsonMapper.ToJson(data);
                return Encoding.UTF8.GetBytes(json);
            }
            catch (Exception ex)
            {
                GF.LogException(ex);
                throw;
            }
        }

        /// <summary>
        /// 反序列化字节数组为数据对象
        /// </summary>
        public T Deserialize<T>(byte[] bytes)
        {
            try
            {
                if (bytes == null || bytes.Length == 0)
                {
                    LogWarning($"[{Name}] 尝试反序列化空数据");
                    return default(T);
                }

                var json = Encoding.UTF8.GetString(bytes);
                return JsonMapper.ToObject<T>(json);
            }
            catch (Exception ex)
            {
                GF.LogException(ex);
                throw;
            }
        }

        /// <summary>
        /// 异步序列化数据为字节数组
        /// </summary>
        public UniTask<byte[]> SerializeAsync<T>(T data, CancellationToken cancellationToken = default)
        {
            return UniTask.FromResult(Serialize(data));
        }

        /// <summary>
        /// 异步反序列化字节数组为数据对象
        /// </summary>
        public UniTask<T> DeserializeAsync<T>(byte[] bytes, CancellationToken cancellationToken = default)
        {
            return UniTask.FromResult(Deserialize<T>(bytes));
        }

        #endregion

        #region 非泛型方法（支持动态类型）

        /// <summary>
        /// 序列化对象为 JSON 字符串
        /// </summary>
        public string SerializeToJson(object data)
        {
            try
            {
                if (data == null)
                {
                    LogWarning($"[{Name}] 尝试序列化空对象");
                    return "{}";
                }

                return JsonMapper.ToJson(data);
            }
            catch (Exception ex)
            {
                GF.LogException(ex);
                throw;
            }
        }

        /// <summary>
        /// 从 JSON 字符串反序列化为指定类型的对象
        /// </summary>
        public object DeserializeFromJson(string json, Type type)
        {
            try
            {
                if (string.IsNullOrEmpty(json))
                {
                    LogWarning($"[{Name}] 尝试反序列化空JSON");
                    return null;
                }

                return JsonMapper.ToObject(json, type);
            }
            catch (Exception ex)
            {
                GF.LogException(ex);
                throw;
            }
        }

        #endregion

        #region 生命周期

        protected override UniTask OnInitAsync()
        {
            return UniTask.CompletedTask;
        }

        #endregion
    }
}