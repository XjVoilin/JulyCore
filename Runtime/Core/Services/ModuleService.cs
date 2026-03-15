using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace JulyCore.Core
{
    /// <summary>
    /// 模块服务实现
    /// 支持优先级排序和增量初始化
    /// </summary>
    internal class ModuleService : IModuleService
    {
        private readonly Dictionary<Type, IModule> _moduleDic = new();
        private readonly List<IModule> _modules = new();

        public bool IsInitialized { get; private set; }

        #region 注册与获取

        public void RegisterModule<T>() where T : IModule, new()
        {
            RegisterModule(new T());
        }

        public void RegisterModule(IModule module)
        {
            if (module == null)
                throw new ArgumentNullException(nameof(module));

            var moduleType = module.GetType();
            if (_moduleDic.ContainsKey(moduleType))
                throw new JulyException($"模块 {moduleType.Name} 已经注册");

            _moduleDic[moduleType] = module;

            int priority = GetPriority(module);
            int insertIndex = _modules.Count;
            for (int i = 0; i < _modules.Count; i++)
            {
                if (priority < GetPriority(_modules[i]))
                {
                    insertIndex = i;
                    break;
                }
            }
            _modules.Insert(insertIndex, module);
        }

        public T GetModule<T>() where T : IModule
        {
            return _moduleDic.TryGetValue(typeof(T), out var value) ? (T)value : default;
        }

        public bool TryGetModule<T>(out T module) where T : IModule
        {
            module = default;
            if (_moduleDic.TryGetValue(typeof(T), out var m))
            {
                module = (T)m;
                return true;
            }
            return false;
        }

        public bool HasModule<T>() where T : IModule
        {
            return _moduleDic.ContainsKey(typeof(T));
        }

        public TCapability GetModuleByCapability<TCapability>() where TCapability : class, ICapability
        {
            var capabilityType = typeof(TCapability);
            
            foreach (var kvp in _moduleDic)
            {
                if (capabilityType.IsAssignableFrom(kvp.Key))
                    return kvp.Value as TCapability;
            }
            
            return null;
        }

        #endregion

        #region 生命周期管理

        public async UniTask InitAllAsync()
        {
            var modules = _modules.ToList();
            var initialized = new List<IModule>();
            var newCount = 0;

            foreach (var module in modules)
            {
                if (!module.IsInitialized)
                {
                    try
                    {
                        await module.InitAsync();
                        await module.EnableAsync();
                        initialized.Add(module);
                        newCount++;
                    }
                    catch (Exception ex)
                    {
                        JLogger.LogError($"{Frameworkconst.TagModuleService} 模块 {module.Name} 初始化失败: {ex.Message}");
                        JLogger.LogException(ex);

                        await RollbackAsync(initialized);

                        throw new JulyException(
                            FrameworkErrorCode.ModuleInitFailed,
                            $"模块 {module.Name} 初始化失败，框架启动终止: {ex.Message}",
                            ex);
                    }
                }
            }

            IsInitialized = true;

            if (newCount > 0)
                JLogger.Log($"{Frameworkconst.TagModuleService} {newCount} 个 Module 初始化完成");
        }

        public async UniTask ShutdownAsync()
        {
            if (!IsInitialized) return;

            for (int i = _modules.Count - 1; i >= 0; i--)
            {
                if (_modules[i].IsInitialized)
                {
                    try
                    {
                        await _modules[i].ShutdownAsync();
                    }
                    catch (Exception ex)
                    {
                        JLogger.LogError(
                            $"{Frameworkconst.TagModuleService} Module {_modules[i].Name} 关闭异常: {ex.Message}");
                    }
                }
            }

            IsInitialized = false;
            JLogger.Log($"{Frameworkconst.TagModuleService} {_modules.Count} 个 Module 已关闭");
        }

        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            if (!IsInitialized) return;

            foreach (var module in _modules)
            {
                if (!module.IsInitialized || !module.IsEnabled) continue;

                try
                {
                    module.Update(elapseSeconds, realElapseSeconds);
                }
                catch (Exception ex)
                {
                    JLogger.LogError($"{Frameworkconst.TagModuleService} Module {module.Name} Update异常: {ex.Message}");
                    JLogger.LogException(ex);
                }
            }
        }

        public void Clear()
        {
            foreach (var module in _modules)
            {
                try { module.Dispose(); }
                catch (Exception ex)
                {
                    JLogger.LogError($"{Frameworkconst.TagModuleService} 释放Module {module.Name} 时异常: {ex.Message}");
                }
            }

            _modules.Clear();
            _moduleDic.Clear();
            IsInitialized = false;
        }

        #endregion

        #region 私有方法

        private async UniTask RollbackAsync(List<IModule> modules)
        {
            if (modules.Count == 0) return;

            JLogger.LogWarning($"{Frameworkconst.TagModuleService} 开始回滚 {modules.Count} 个已初始化的模块");

            for (int i = modules.Count - 1; i >= 0; i--)
            {
                try
                {
                    await modules[i].ShutdownAsync();
                }
                catch (Exception ex)
                {
                    JLogger.LogError($"{Frameworkconst.TagModuleService} 回滚模块时异常: {ex.Message}");
                }
            }

            JLogger.LogWarning($"{Frameworkconst.TagModuleService} 模块回滚完成");
        }

        private int GetPriority(IModule module)
        {
            return module is IPriority p ? p.Priority : module.Priority;
        }

        #endregion
    }
}
