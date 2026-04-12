using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Data.HotUpdate;
using JulyCore.Module.Base;
using JulyCore.Provider.HotUpdate;

namespace JulyCore.Module.HotUpdate
{
    /// <summary>
    /// 热更新加载状态
    /// </summary>
    public enum HotUpdateState
    {
        /// <summary>
        /// 未加载
        /// </summary>
        NotLoaded,

        /// <summary>
        /// 加载中
        /// </summary>
        Loading,

        /// <summary>
        /// 已加载
        /// </summary>
        Loaded,

        /// <summary>
        /// 加载失败
        /// </summary>
        Failed
    }

    /// <summary>
    /// 热更新状态变化事件
    /// </summary>
    public class HotUpdateStateChangedEvent : IEvent
    {
        /// <summary>
        /// 旧状态
        /// </summary>
        public HotUpdateState OldState { get; set; }

        /// <summary>
        /// 新状态
        /// </summary>
        public HotUpdateState NewState { get; set; }

        /// <summary>
        /// 错误信息（仅失败时有值）
        /// </summary>
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// 热更新进度事件
    /// </summary>
    public class HotUpdateProgressEvent : IEvent
    {
        /// <summary>
        /// 当前阶段
        /// </summary>
        public HotUpdateStage Stage { get; set; }

        /// <summary>
        /// 当前进度（0-1）
        /// </summary>
        public float Progress { get; set; }

        /// <summary>
        /// 描述信息
        /// </summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// 热更新模块
    /// 
    /// 【职责】
    /// - 业务语义与流程调度：决定何时加载热更新、管理加载状态
    /// - 状态变化通知：通过 EventBus 发布热更新状态和进度事件
    /// 
    /// 【通信模式】
    /// - 调用 Provider：执行技术操作（程序集加载、资源下载等）
    /// - 发布 Event：通知外部热更新状态变化（供 UI 显示进度、其他模块响应）
    /// </summary>
    internal class HotUpdateModule : ModuleBase
    {
        private IHotUpdateProvider _hotUpdateProvider;
        private HotUpdateState _state = HotUpdateState.NotLoaded;
        private HotUpdateConfig _config;
        private HotUpdateResult _lastResult;

        protected override LogChannel LogChannel => LogChannel.HotUpdate;

        /// <summary>
        /// 模块执行优先级（热更新模块应最先执行）
        /// </summary>
        public override int Priority => Frameworkconst.PriorityHotUpdateModule;

        /// <summary>
        /// 当前热更新状态
        /// </summary>
        internal HotUpdateState State => _state;

        /// <summary>
        /// 最后一次加载结果
        /// </summary>
        internal HotUpdateResult LastResult => _lastResult;

        /// <summary>
        /// 是否已加载热更新
        /// </summary>
        internal bool IsLoaded => _state == HotUpdateState.Loaded;

        /// <summary>
        /// 已加载的程序集列表
        /// </summary>
        internal IReadOnlyList<Assembly> LoadedAssemblies =>
            _hotUpdateProvider?.LoadedAssemblies ?? Array.Empty<Assembly>();

        protected override UniTask OnInitAsync()
        {
            try
            {
                _hotUpdateProvider = GetProvider<IHotUpdateProvider>();
                if (_hotUpdateProvider == null)
                {
                    throw new JulyException($"[{Name}] 需要IHotUpdateProvider，请先注册IHotUpdateProvider");
                }

                return base.OnInitAsync();
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 热更新模块初始化失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 使用配置加载热更新
        /// </summary>
        /// <param name="config">热更新配置</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>加载结果</returns>
        internal async UniTask<HotUpdateResult> LoadAsync(HotUpdateConfig config,
            CancellationToken cancellationToken = default)
        {
            if (config == null)
            {
                return HotUpdateResult.Failure("配置不能为空");
            }

            if (_state == HotUpdateState.Loading)
            {
                LogWarning($"[{Name}] 热更新正在加载中，请勿重复调用");
                return HotUpdateResult.Failure("正在加载中");
            }

            if (_state == HotUpdateState.Loaded)
            {
                LogWarning($"[{Name}] 热更新已加载，无需重复加载");
                return _lastResult ?? HotUpdateResult.Success(new List<Assembly>());
            }

            _config = config;
            SetState(HotUpdateState.Loading);

            try
            {
                // 调用Provider加载
                var result = await _hotUpdateProvider.LoadHotUpdateAsync(config, OnProgressCallback, cancellationToken);

                _lastResult = result;

                if (result.IsSuccess)
                {
                    SetState(HotUpdateState.Loaded);
                }
                else
                {
                    SetState(HotUpdateState.Failed, result.ErrorMessage);
                    LogError($"[{Name}] 热更新加载失败: {result.ErrorMessage}");
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                SetState(HotUpdateState.Failed, "加载已取消");
                return HotUpdateResult.Failure("加载已取消");
            }
            catch (Exception ex)
            {
                SetState(HotUpdateState.Failed, ex.Message);
                LogError($"[{Name}] 热更新加载异常: {ex.Message}");
                JLogger.LogException(ex);
                return HotUpdateResult.Failure(ex.Message);
            }
        }

        /// <summary>
        /// 使用默认配置快速加载热更新
        /// </summary>
        /// <param name="hotUpdateAssemblies">热更新程序集名称列表</param>
        /// <param name="aotMetaAssemblies">AOT元数据程序集名称列表</param>
        /// <param name="entryClass">入口类全名</param>
        /// <param name="entryMethod">入口方法名</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>加载结果</returns>
        internal UniTask<HotUpdateResult> LoadAsync(
            List<string> hotUpdateAssemblies,
            List<string> aotMetaAssemblies = null,
            string entryClass = null,
            string entryMethod = "Start",
            CancellationToken cancellationToken = default)
        {
            var config = new HotUpdateConfig
            {
                HotUpdateAssemblyNames = hotUpdateAssemblies ?? new List<string>(),
                AOTMetaAssemblyNames = aotMetaAssemblies ?? new List<string>(),
                EntryClassName = entryClass,
                EntryMethodName = entryMethod
            };

            return LoadAsync(config, cancellationToken);
        }

        /// <summary>
        /// 执行热更新入口方法
        /// </summary>
        /// <param name="entryClassName">入口类全名</param>
        /// <param name="entryMethodName">入口方法名</param>
        /// <param name="parameters">方法参数</param>
        /// <returns>是否执行成功</returns>
        internal bool ExecuteEntry(string entryClassName, string entryMethodName, object[] parameters = null)
        {
            if (_state != HotUpdateState.Loaded)
            {
                LogWarning($"[{Name}] 热更新未加载，无法执行入口方法");
                return false;
            }

            return _hotUpdateProvider.ExecuteEntry(entryClassName, entryMethodName, parameters);
        }

        /// <summary>
        /// 从热更新程序集获取类型
        /// </summary>
        /// <param name="typeFullName">类型全名</param>
        /// <returns>类型，未找到返回null</returns>
        internal Type GetType(string typeFullName)
        {
            return _hotUpdateProvider?.GetHotUpdateType(typeFullName);
        }

        /// <summary>
        /// 从热更新程序集获取所有符合条件的类型
        /// </summary>
        /// <param name="predicate">筛选条件</param>
        /// <returns>类型列表</returns>
        internal List<Type> GetTypes(Func<Type, bool> predicate = null)
        {
            return _hotUpdateProvider?.GetHotUpdateTypes(predicate) ?? new List<Type>();
        }

        /// <summary>
        /// 创建热更新类型实例
        /// </summary>
        /// <typeparam name="T">基类或接口类型</typeparam>
        /// <param name="typeFullName">类型全名</param>
        /// <param name="args">构造函数参数</param>
        /// <returns>实例</returns>
        internal T CreateInstance<T>(string typeFullName, params object[] args) where T : class
        {
            var type = GetType(typeFullName);
            if (type == null)
            {
                LogWarning($"[{Name}] 未找到类型: {typeFullName}");
                return null;
            }

            try
            {
                var instance = Activator.CreateInstance(type, args);
                return instance as T;
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 创建实例失败: {typeFullName}, 错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 重置热更新状态（用于调试或重新加载）
        /// </summary>
        internal void Reset()
        {
            _state = HotUpdateState.NotLoaded;
            _lastResult = null;
            _config = null;
        }

        #region 私有辅助方法

        private void SetState(HotUpdateState newState, string errorMessage = null)
        {
            var oldState = _state;
            _state = newState;

            // 发布状态变化事件
            try
            {
                EventBus.Publish(new HotUpdateStateChangedEvent
                {
                    OldState = oldState,
                    NewState = newState,
                    ErrorMessage = errorMessage
                });
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 发布状态变化事件失败: {ex.Message}");
            }
        }

        private void OnProgressCallback(HotUpdateProgress progress)
        {
            // 发布进度事件
            try
            {
                EventBus.Publish(new HotUpdateProgressEvent
                {
                    Stage = progress.Stage,
                    Progress = progress.Progress,
                    Description = progress.Description
                });
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 发布进度事件失败: {ex.Message}");
            }
        }

        #endregion

        protected override void OnShutdown()
        {
            _hotUpdateProvider = null;
            _config = null;
            _lastResult = null;
            _state = HotUpdateState.NotLoaded;
        }
    }
}