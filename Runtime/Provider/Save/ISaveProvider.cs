using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Data.Save;

namespace JulyCore.Provider.Save
{
    /// <summary>
    /// 存档提供者接口
    /// 
    /// 【职责】
    /// - 数据注册管理：维护哪些数据需要被存档系统管理
    /// - 脏标记管理：追踪哪些数据发生了变更
    /// - IO 操作：执行实际的文件读写
    /// 
    /// 【设计说明】
    /// Provider 负责数据的获取、缓存、查询、修改、持久化
    /// Module 负责保存策略（何时保存、保存哪些）
    /// </summary>
    public interface ISaveProvider : Core.IProvider
    {
        #region 数据注册管理

        /// <summary>
        /// 注册存档数据
        /// </summary>
        /// <param name="key">存档键（唯一标识）</param>
        /// <param name="data">存档数据</param>
        void Register(string key, ISaveData data);

        /// <summary>
        /// 注销存档数据
        /// </summary>
        /// <param name="key">存档键</param>
        /// <returns>是否注销成功</returns>
        bool Unregister(string key);

        /// <summary>
        /// 检查数据是否已注册
        /// </summary>
        /// <param name="key">存档键</param>
        /// <returns>是否已注册</returns>
        bool IsRegistered(string key);

        /// <summary>
        /// 获取已注册的存档数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="key">存档键</param>
        /// <returns>存档数据，不存在返回 null</returns>
        T GetRegisteredData<T>(string key) where T : class, ISaveData;

        /// <summary>
        /// 获取所有已注册的存档键
        /// </summary>
        /// <returns>存档键集合</returns>
        IEnumerable<string> GetAllRegisteredKeys();

        #endregion

        #region 脏标记管理

        /// <summary>
        /// 标记数据为脏
        /// </summary>
        /// <param name="key">存档键</param>
        /// <returns>是否标记成功（数据未注册则返回 false）</returns>
        bool MarkDirty(string key);

        /// <summary>
        /// 检查数据是否为脏
        /// </summary>
        /// <param name="key">存档键</param>
        /// <returns>是否为脏</returns>
        bool IsDirty(string key);

        /// <summary>
        /// 获取所有脏数据的键
        /// </summary>
        /// <returns>脏数据键集合</returns>
        IEnumerable<string> GetDirtyKeys();

        /// <summary>
        /// 获取当前脏数据数量
        /// </summary>
        int DirtyCount { get; }

        /// <summary>
        /// 清除指定数据的脏标记
        /// </summary>
        /// <param name="key">存档键</param>
        void ClearDirty(string key);

        /// <summary>
        /// 清除所有脏标记
        /// </summary>
        void ClearAllDirty();

        #endregion

        #region 加载与保存

        /// <summary>
        /// 加载数据并自动注册
        /// </summary>
        /// <typeparam name="T">数据类型（必须实现 ISaveData 接口且有无参构造函数）</typeparam>
        /// <param name="key">存档键</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>加载的数据（不存在则创建新实例并注册）</returns>
        UniTask<T> LoadAndRegisterAsync<T>(string key, CancellationToken cancellationToken = default) 
            where T : ISaveData, new();

        /// <summary>
        /// 批量加载数据并自动注册
        /// </summary>
        /// <typeparam name="T">数据类型（必须实现 ISaveData 接口且有无参构造函数）</typeparam>
        /// <param name="keys">存档键数组</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>加载的数据字典（不存在的键会创建新实例并注册）</returns>
        UniTask<Dictionary<string, T>> LoadAndRegisterBatchAsync<T>(string[] keys, CancellationToken cancellationToken = default) 
            where T : ISaveData, new();

        /// <summary>
        /// 保存数据（异步）
        /// </summary>
        /// <typeparam name="T">数据类型（必须实现ISaveData接口）</typeparam>
        /// <param name="key">存档键（唯一标识）</param>
        /// <param name="data">要保存的数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>保存结果，包含成功状态和失败原因</returns>
        UniTask<SaveResult> SaveAsync<T>(string key, T data, CancellationToken cancellationToken = default) where T : ISaveData;

        /// <summary>
        /// 加载数据（异步）
        /// </summary>
        /// <typeparam name="T">数据类型（必须实现ISaveData接口）</typeparam>
        /// <param name="key">存档键（唯一标识）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>加载的数据，如果不存在则返回default(T)</returns>
        UniTask<T> LoadAsync<T>(string key, CancellationToken cancellationToken = default) where T : ISaveData;

        /// <summary>
        /// 批量保存已注册的脏数据
        /// </summary>
        /// <param name="keys">要保存的存档键（如果为 null，则保存所有脏数据）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>保存结果字典</returns>
        UniTask<Dictionary<string, SaveResult>> SaveRegisteredAsync(
            IEnumerable<string> keys = null, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 批量保存数据（异步）
        /// </summary>
        /// <typeparam name="T">数据类型（必须实现ISaveData接口）</typeparam>
        /// <param name="dataMap">数据字典，key为存档键，value为要保存的数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>保存结果字典，key为存档键，value为对应的保存结果</returns>
        UniTask<Dictionary<string, SaveResult>> SaveBatchAsync<T>(Dictionary<string, T> dataMap, CancellationToken cancellationToken = default) where T : ISaveData;

        /// <summary>
        /// 批量加载数据（异步）
        /// </summary>
        /// <typeparam name="T">数据类型（必须实现ISaveData接口）</typeparam>
        /// <param name="keys">存档键数组</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>加载结果字典，key为存档键，value为对应的数据（不存在则为default(T)）</returns>
        UniTask<Dictionary<string, T>> LoadBatchAsync<T>(string[] keys, CancellationToken cancellationToken = default) where T : ISaveData;

        #endregion

        #region 文件操作

        /// <summary>
        /// 删除存档
        /// </summary>
        /// <param name="key">存档键（唯一标识）</param>
        /// <returns>是否删除成功</returns>
        bool Delete(string key);

        /// <summary>
        /// 检查存档是否存在
        /// </summary>
        /// <param name="key">存档键（唯一标识）</param>
        /// <returns>是否存在</returns>
        bool HasSave(string key);

        #endregion
    }
}

