using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Data.Save;
using JulyCore.Module.Base;
using JulyCore.Provider.Save;

namespace JulyCore.Module.Save
{
    /// <summary>
    /// 存档模块
    /// 
    /// 【职责】
    /// - 保存策略管理（ISaveStrategy）
    /// - 保存时机调度（定时保存、信号触发、关闭时兜底）
    /// - 不持有数据，数据管理由 ISaveProvider 负责
    /// 
    /// 【设计说明】
    /// - 基础的数据注册、脏标记、加载/保存操作直接通过 ISaveProvider 完成
    /// - 其他 Module/Provider 可直接依赖 ISaveProvider，无需通过此模块
    /// - 此模块专注于保存策略和调度
    /// </summary>
    internal class SaveModule : ModuleBase
    {
        private ISaveProvider _saveProvider;

        /// <summary>
        /// 日志通道
        /// </summary>
        protected override LogChannel LogChannel => LogChannel.Save;

        /// <summary>
        /// 保存决策策略
        /// </summary>
        private ISaveStrategy _saveStrategy;

        /// <summary>
        /// 定时保存间隔（秒）
        /// </summary>
        private const float AutoSaveInterval = 30f;

        /// <summary>
        /// 上次自动保存时间
        /// </summary>
        private float _lastAutoSaveTime;

        /// <summary>
        /// 是否正在保存
        /// </summary>
        private bool _isSaving;

        /// <summary>
        /// 模块执行优先级
        /// </summary>
        public override int Priority => Frameworkconst.PrioritySaveModule;

        protected override UniTask OnInitAsync()
        {
            try
            {
                _saveProvider = GetProvider<ISaveProvider>();
                // 设置默认策略
                _saveStrategy = new ImportanceBasedSaveStrategy();

                return base.OnInitAsync();
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 存档模块初始化失败: {ex.Message}");
                throw;
            }
        }

        #region 策略管理

        /// <summary>
        /// 设置保存决策策略
        /// </summary>
        internal void SetPolicy(ISaveStrategy strategy)
        {
            if (strategy == null)
            {
                return;
            }

            _saveStrategy = strategy;
        }

        /// <summary>
        /// 获取当前保存策略
        /// </summary>
        internal ISaveStrategy GetPolicy()
        {
            return _saveStrategy;
        }

        #endregion

        #region 保存信号处理

        /// <summary>
        /// 触发保存
        /// </summary>
        /// <param name="signal">保存信号级别</param>
        /// <returns>保存结果字典</returns>
        internal async UniTask<Dictionary<string, SaveResult>> TriggerSaveAsync(SaveSignal signal)
        {
            var keysToSave = GetKeysToSave(signal);
            if (keysToSave.Count == 0)
            {
                return new Dictionary<string, SaveResult>();
            }

            var results = await _saveProvider.SaveRegisteredAsync(keysToSave, GFCancellationToken);
            return results;
        }

        /// <summary>
        /// 根据保存信号和策略获取需要保存的键
        /// </summary>
        private List<string> GetKeysToSave(SaveSignal signal)
        {
            var result = new List<string>();
            var dirtyKeys = _saveProvider.GetDirtyKeys();

            foreach (var key in dirtyKeys)
            {
                var data = _saveProvider.GetRegisteredData<ISaveData>(key);
                if (data == null)
                {
                    continue;
                }

                var context = new SaveContext(signal, key, data);
                if (_saveStrategy.ShouldSave(context))
                {
                    result.Add(key);
                }
            }

            return result;
        }

        #endregion

        #region 直接 IO 操作（代理到 Provider）

        /// <summary>
        /// 检查存档是否存在
        /// </summary>
        internal bool HasSave(string key)
        {
            return _saveProvider.HasSave(key);
        }

        #endregion

        #region 生命周期管理

        protected override UniTask OnEnableAsync()
        {
            _lastAutoSaveTime = 0f;
            _isSaving = false;
            return base.OnEnableAsync();
        }

        protected override void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            _lastAutoSaveTime += realElapseSeconds;

            if (_lastAutoSaveTime >= AutoSaveInterval && !_isSaving)
            {
                if (_saveProvider.DirtyCount > 0)
                {
                    _lastAutoSaveTime = 0f;
                    _isSaving = true;

                    TriggerSaveAsync(SaveSignal.Low)
                        .ContinueWith(_ => { _isSaving = false; })
                        .Forget();
                }
            }

            base.OnUpdate(elapseSeconds, realElapseSeconds);
        }

        protected override async UniTask OnShutdownAsync()
        {
            var dirtyCount = _saveProvider.DirtyCount;

            if (dirtyCount > 0)
            {
                await TriggerSaveAsync(SaveSignal.Immediate);
            }

            await base.OnShutdownAsync();
        }

        #endregion

        #region 数据注册管理（代理到 Provider）

        /// <summary>
        /// 注册存档数据
        /// </summary>
        internal void Register(string key, ISaveData data)
        {
            _saveProvider.Register(key, data);
        }

        /// <summary>
        /// 注销存档数据
        /// </summary>
        internal bool Unregister(string key)
        {
            return _saveProvider.Unregister(key);
        }

        /// <summary>
        /// 检查数据是否已注册
        /// </summary>
        internal bool IsRegistered(string key)
        {
            return _saveProvider.IsRegistered(key);
        }

        /// <summary>
        /// 获取已注册的存档数据
        /// </summary>
        internal T GetRegisteredData<T>(string key) where T : class, ISaveData
        {
            return _saveProvider.GetRegisteredData<T>(key);
        }

        #endregion

        #region 脏标记管理（代理到 Provider）

        /// <summary>
        /// 标记数据为脏
        /// </summary>
        internal bool MarkDirty(string key)
        {
            return _saveProvider.MarkDirty(key);
        }

        /// <summary>
        /// 检查数据是否为脏
        /// </summary>
        internal bool IsDirty(string key)
        {
            return _saveProvider.IsDirty(key);
        }

        /// <summary>
        /// 获取当前脏数据数量
        /// </summary>
        internal int DirtyCount => _saveProvider.DirtyCount;

        #endregion

        #region 内部方法（供门面类调用）

        /// <summary>
        /// 加载并注册单个数据
        /// </summary>
        internal UniTask<T> LoadAndRegisterAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : ISaveData, new()
        {
            return _saveProvider.LoadAndRegisterAsync<T>(key, cancellationToken);
        }

        /// <summary>
        /// 加载单个数据
        /// </summary>
        /// <param name="key"></param>
        /// <param name="cancellationToken"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        internal UniTask<T> LoadAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : ISaveData, new()
        {
            return _saveProvider.LoadAsync<T>(key, cancellationToken);
        }

        /// <summary>
        /// 批量加载并注册数据
        /// </summary>
        internal UniTask<Dictionary<string, T>> LoadAndRegisterBatchAsync<T>(string[] keys, CancellationToken cancellationToken = default)
            where T : ISaveData, new()
        {
            return _saveProvider.LoadAndRegisterBatchAsync<T>(keys, cancellationToken);
        }

        /// <summary>
        /// 删除存档
        /// </summary>
        internal bool DeleteAsync(string key)
        {
            return _saveProvider.Delete(key);
        }

        /// <summary>
        /// 标记数据为脏，并根据信号级别决定保存时机
        /// </summary>
        /// <param name="key">存档键</param>
        /// <param name="signal">保存信号级别</param>
        /// <returns>是否成功</returns>
        internal async UniTask<bool> MarkDirtyAndSaveAsync(string key, SaveSignal signal)
        {
            // 先标记为脏
            if (!_saveProvider.MarkDirty(key))
            {
                return false;
            }

            switch (signal)
            {
                case SaveSignal.Low:
                    // 仅标记，等待自动保存
                    return true;

                case SaveSignal.Medium:
                    // 累积到一定数量后保存
                    if (_saveProvider.DirtyCount < Frameworkconst.MediumDirtyCount) 
                        return true;
                    var results = await TriggerSaveAsync(signal);
                    return results.TryGetValue(key, out var result) && result.Success;

                case SaveSignal.High:
                case SaveSignal.Immediate:
                    // 立即触发保存
                    var saveResults = await TriggerSaveAsync(signal);
                    return saveResults.TryGetValue(key, out var saveResult) && saveResult.Success;

                default:
                    return true;
            }
        }

        #endregion
    }
}
