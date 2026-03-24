using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Module.Base;
using JulyCore.Provider.Data;

namespace JulyCore.Module.Data
{
    /// <summary>
    /// 序列化模块
    /// 纯技术层代理：Serialize是纯技术实现（序列化/反序列化）
    /// 如果未来需要业务规则（如序列化格式选择、版本兼容性处理），可在此层添加
    /// </summary>
    internal class SerializeModule : ModuleBase
    {
        private ISerializeProvider _serializeProvider;

        protected override LogChannel LogChannel => LogChannel.Serialize;

        /// <summary>
        /// 模块执行优先级
        /// </summary>
        public override int Priority => Frameworkconst.PrioritySerializeModule;

        /// <summary>
        /// 初始化Module
        /// </summary>
        protected override UniTask OnInitAsync()
        {
            try
            {
                // 获取数据提供者
                _serializeProvider = GetProvider<ISerializeProvider>();
                return base.OnInitAsync();
            }
            catch (Exception ex)
            {
                GF.LogException(ex);
                throw;
            }
        }

        /// <summary>
        /// 序列化数据（使用当前提供者）
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="data">要序列化的数据</param>
        /// <returns>序列化后的字节数组</returns>
        /// <exception cref="InvalidOperationException">当Provider未初始化时抛出</exception>
        internal byte[] Serialize<T>(T data)
        {
            if (_serializeProvider == null)
            {
                throw new InvalidOperationException($"[{Name}] 数据提供者未初始化，无法序列化数据");
            }

            try
            {
                return _serializeProvider.Serialize(data);
            }
            catch (Exception ex)
            {
                GF.LogException(ex);
                throw;
            }
        }

        /// <summary>
        /// 反序列化数据（使用当前提供者）
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="bytes">要反序列化的字节数组</param>
        /// <returns>反序列化后的数据对象</returns>
        /// <exception cref="InvalidOperationException">当Provider未初始化时抛出</exception>
        internal T Deserialize<T>(byte[] bytes)
        {
            if (_serializeProvider == null)
            {
                throw new InvalidOperationException($"[{Name}] 数据提供者未初始化，无法反序列化数据");
            }

            if (bytes == null || bytes.Length == 0)
            {
                LogWarning($"[{Name}] 尝试反序列化空数据");
                return default(T);
            }

            try
            {
                return _serializeProvider.Deserialize<T>(bytes);
            }
            catch (Exception ex)
            {
                GF.LogException(ex);
                throw;
            }
        }

        /// <summary>
        /// 异步序列化数据（使用当前提供者）
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="data">要序列化的数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>序列化后的字节数组</returns>
        /// <exception cref="InvalidOperationException">当Provider未初始化时抛出</exception>
        internal UniTask<byte[]> SerializeAsync<T>(T data, CancellationToken cancellationToken = default)
        {
            if (_serializeProvider == null)
            {
                throw new InvalidOperationException($"[{Name}] 数据提供者未初始化，无法序列化数据");
            }

            try
            {
                return _serializeProvider.SerializeAsync(data, cancellationToken);
            }
            catch (Exception ex)
            {
                GF.LogException(ex);
                return UniTask.FromException<byte[]>(ex);
            }
        }

        /// <summary>
        /// 异步反序列化数据（使用当前提供者）
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="bytes">要反序列化的字节数组</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>反序列化后的数据对象</returns>
        /// <exception cref="InvalidOperationException">当Provider未初始化时抛出</exception>
        internal UniTask<T> DeserializeAsync<T>(byte[] bytes, CancellationToken cancellationToken = default)
        {
            if (_serializeProvider == null)
            {
                throw new InvalidOperationException($"[{Name}] 数据提供者未初始化，无法反序列化数据");
            }

            if (bytes == null || bytes.Length == 0)
            {
                LogWarning($"[{Name}] 尝试异步反序列化空数据");
                return UniTask.FromResult<T>(default(T));
            }

            try
            {
                return _serializeProvider.DeserializeAsync<T>(bytes, cancellationToken);
            }
            catch (Exception ex)
            {
                GF.LogException(ex);
                return UniTask.FromException<T>(ex);
            }
        }

        /// <summary>
        /// 关闭Module
        /// </summary>
        protected override UniTask OnShutdownAsync()
        {
            if (_serializeProvider != null && _serializeProvider.IsInitialized)
            {
                try
                {
                    _serializeProvider.Shutdown();
                }
                catch (Exception ex)
                {
                    GF.LogException(ex);
                }
            }

            _serializeProvider = null;
            return UniTask.CompletedTask;
        }
    }
}