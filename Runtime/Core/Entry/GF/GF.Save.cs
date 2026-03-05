using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Data.Save;
using JulyCore.Module.Save;
using JulyCore.Provider.Save;

namespace JulyCore
{
    public static partial class GF
    {
        /// <summary>
        /// 存档相关操作
        /// 
        /// 【使用流程】
        /// 1. （可选）调用 SetPolicy() 设置自定义保存策略
        /// 2. 调用 Register(key, data) 注册存档数据
        /// 3. 数据变化后，调用 MarkDirty(key) 标记脏数据
        /// 4. 在适当时机，调用 TriggerSaveAsync(signal) 发出保存信号
        /// 
        /// 【设计说明】
        /// - 框架层只响应抽象的保存信号，不感知任何具体业务事件
        /// - 保存决策委托给 ISavePolicy 策略对象（默认使用 ImportanceBasedSavePolicy）
        /// - 策略可以组合、替换，支持灵活的保存规则配置
        /// - 定时保存（每30秒）自动触发 Low 级别信号
        /// - 退出时自动触发 Immediate 级别信号（兜底保存）
        /// 
        /// 【默认策略规则】（ImportanceBasedSavePolicy）
        /// - Low 信号：仅保存 Critical 级别数据
        /// - Medium 信号：保存 Critical + Important 级别数据
        /// - High 信号：保存 Critical + Important + Normal 级别数据
        /// - Immediate 信号：保存所有脏数据
        /// </summary>
        public static class Save
        {
            private static SaveModule _module;

            private static SaveModule Module
            {
                get
                {
                    if (_module == null)
                    {
                        _module = GetModule<SaveModule>();
                    }

                    return _module;
                }
            }

            #region 策略管理

            /// <summary>
            /// 获取当前保存策略
            /// </summary>
            /// <returns>当前使用的保存策略</returns>
            public static ISaveStrategy GetPolicy()
            {
                return Module.GetPolicy();
            }

            #endregion

            #region 数据注册管理

            /// <summary>
            /// 注册存档数据
            /// 
            /// 将数据对象注册到存档系统，之后可通过 MarkDirty 标记脏数据
            /// </summary>
            /// <param name="key">存档键（唯一标识）</param>
            /// <param name="data">存档数据对象</param>
            /// <example>
            /// var playerData = new PlayerData { Level = 1, Gold = 100 };
            /// GF.Save.Register("player", playerData);
            /// </example>
            public static void Register(string key, ISaveData data)
            {
                Module.Register(key, data);
            }

            /// <summary>
            /// 注销存档数据
            /// </summary>
            /// <param name="key">存档键</param>
            /// <returns>是否成功注销</returns>
            public static bool Unregister(string key)
            {
                return Module.Unregister(key);
            }

            /// <summary>
            /// 检查数据是否已注册
            /// </summary>
            /// <param name="key">存档键</param>
            /// <returns>是否已注册</returns>
            public static bool IsRegistered(string key)
            {
                return Module.IsRegistered(key);
            }

            /// <summary>
            /// 获取已注册的存档数据
            /// </summary>
            /// <typeparam name="T">数据类型</typeparam>
            /// <param name="key">存档键</param>
            /// <returns>数据对象，未注册则返回 null</returns>
            public static T GetRegisteredData<T>(string key) where T : class, ISaveData
            {
                return Module.GetRegisteredData<T>(key);
            }

            #endregion

            #region 脏标记管理

            /// <summary>
            /// 标记数据为脏
            /// 
            /// 数据变化后应调用此方法，框架会在下次保存信号时保存该数据
            /// </summary>
            /// <param name="key">存档键</param>
            /// <returns>是否标记成功</returns>
            /// <example>
            /// playerData.Gold += 100;
            /// GF.Save.MarkDirty("player");
            /// </example>
            public static bool MarkDirty(string key)
            {
                return Module.MarkDirty(key);
            }

            /// <summary>
            /// 检查数据是否为脏
            /// </summary>
            /// <param name="key">存档键</param>
            /// <returns>是否为脏</returns>
            public static bool IsDirty(string key)
            {
                return Module.IsDirty(key);
            }

            /// <summary>
            /// 获取当前脏数据数量
            /// </summary>
            public static int DirtyCount => Module.DirtyCount;

            #endregion

            #region 保存信号

            /// <summary>
            /// 触发保存信号
            /// 
            /// 委托策略对象判断哪些脏数据需要在当前信号下保存
            /// </summary>
            /// <param name="signal">保存信号级别</param>
            /// <returns>保存结果字典</returns>
            public static UniTask<Dictionary<string, SaveResult>> TriggerSaveAsync(
                SaveSignal signal)
            {
                return Module.TriggerSaveAsync(signal);
            }

            #endregion

            #region 直接 IO 操作

            /// <summary>
            /// 加载并注册单个数据
            /// 如果存档不存在，会创建新实例并注册
            /// </summary>
            /// <typeparam name="T">数据类型</typeparam>
            /// <param name="key">存档键</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>加载并注册后的数据</returns>
            public static UniTask<T> LoadAndRegisterAsync<T>(
                string key,
                CancellationToken cancellationToken = default) where T : ISaveData, new()
            {
                return Module.LoadAndRegisterAsync<T>(key, cancellationToken);
            }

            /// <summary>
            /// 批量加载并注册数据（推荐）
            /// 一步完成批量加载和注册操作，简化上层调用。
            /// 可通过工厂方法为不存在的存档提供默认值。
            /// </summary>
            /// <typeparam name="T">数据类型</typeparam>
            /// <param name="keys">存档键数组</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>加载并注册后的数据字典</returns>
            public static UniTask<Dictionary<string, T>> LoadAndRegisterBatchAsync<T>(
                string[] keys,
                CancellationToken cancellationToken = default) where T : ISaveData, new()
            {
                return Module.LoadAndRegisterBatchAsync<T>(keys, cancellationToken);
            }
            
            /// <summary>
            /// 加载并注册单个数据
            /// 如果存档不存在，会创建新实例并注册
            /// </summary>
            /// <typeparam name="T">数据类型</typeparam>
            /// <param name="key">存档键</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>加载并注册后的数据</returns>
            public static UniTask<T> LoadAsync<T>(
                string key,
                CancellationToken cancellationToken = default) where T : ISaveData, new()
            {
                return Module.LoadAsync<T>(key, cancellationToken);
            }

            /// <summary>
            /// 删除存档
            /// </summary>
            /// <param name="key">存档键</param>
            /// <returns>是否删除成功</returns>
            public static bool DeleteAsync(string key)
            {
                return Module.DeleteAsync(key);
            }

            /// <summary>
            /// 检查存档是否存在
            /// </summary>
            /// <param name="key">存档键</param>
            /// <returns>是否存在</returns>
            public static bool HasSave(string key)
            {
                return Module.HasSave(key);
            }

            #endregion
        }
    }
}