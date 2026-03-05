using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace JulyCore.Core
{
    /// <summary>
    /// 模块服务实现
    /// 线程安全，支持依赖注入和优先级排序
    /// </summary>
    internal class ModuleService : IModuleService
    {
        private readonly object _lock = new object();
        private readonly ConcurrentDictionary<Type, IModule> _moduleDic = new();
        private readonly List<IModule> _modules = new();
        private IModule[] _cachedSnapshot = Array.Empty<IModule>();
        private bool _cacheInvalid = true;
        
        /// <summary>
        /// DI 容器引用，用于自动注册 Capability
        /// </summary>
        private IDependencyContainer _container;

        public bool IsInitialized { get; private set; }
        
        /// <summary>
        /// 设置 DI 容器（由 FrameworkContext 在创建后调用）
        /// </summary>
        internal void SetContainer(IDependencyContainer container)
        {
            _container = container;
        }

        #region 注册与获取

        public void RegisterModule<T>() where T : IModule, new()
        {
            RegisterModule(new T());
        }

        public void RegisterModule(IModule module)
        {
            if (module == null)
            {
                throw new ArgumentNullException(nameof(module));
            }

            var moduleType = module.GetType();
            if (!_moduleDic.TryAdd(moduleType, module))
            {
                throw new JulyException($"模块 {moduleType.Name} 已经注册");
            }

            lock (_lock)
            {
                // 按优先级插入
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
                _cacheInvalid = true;
            }

            // 自动注册 Module 实现的 Capability 接口到 DI 容器
            RegisterCapabilitiesToContainer(module);

            JLogger.Log($"{Frameworkconst.TagModuleService} 注册模块: {moduleType.Name} (优先级: {module.Priority})");
        }

        /// <summary>
        /// 将 Module 实现的 Capability 接口注册到 DI 容器
        /// 使 Provider 可以通过构造函数注入这些 Capability
        /// </summary>
        private void RegisterCapabilitiesToContainer(IModule module)
        {
            if (_container == null) return;

            var moduleType = module.GetType();
            
            foreach (var interfaceType in moduleType.GetInterfaces())
            {
                // 只注册 ICapability 的直接子接口
                if (IsCapabilityInterface(interfaceType))
                {
                    _container.RegisterSingleton(interfaceType, module);
                    JLogger.Log($"{Frameworkconst.TagModuleService} 注册 Capability: {interfaceType.Name} -> {moduleType.Name}");
                }
            }
        }

        /// <summary>
        /// 判断是否为有效的 Capability 接口（ICapability 的直接子接口）
        /// </summary>
        private static bool IsCapabilityInterface(Type interfaceType)
        {
            // 必须继承自 ICapability
            if (!typeof(ICapability).IsAssignableFrom(interfaceType))
                return false;

            // 排除 ICapability 本身
            if (interfaceType == typeof(ICapability))
                return false;

            // 排除框架内部接口
            if (interfaceType == typeof(IModule) ||
                interfaceType == typeof(IProvider) ||
                interfaceType == typeof(IModuleDependency) ||
                interfaceType == typeof(IPriority) ||
                interfaceType == typeof(IDisposable))
                return false;

            return true;
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
            
            // 遍历所有已注册的 Module，查找实现了指定接口的 Module
            foreach (var kvp in _moduleDic)
            {
                if (capabilityType.IsAssignableFrom(kvp.Key))
                {
                    return kvp.Value as TCapability;
                }
            }
            
            return null;
        }

        #endregion

        #region 生命周期管理

        public async UniTask InitAllAsync()
        {
            if (IsInitialized)
            {
                JLogger.LogWarning($"{Frameworkconst.TagModuleService} Module已经初始化，跳过重复初始化");
                return;
            }

            // 解析依赖并排序
            var modules = ResolveDependencies();
            JLogger.Log($"{Frameworkconst.TagModuleService} 开始初始化 {modules.Count} 个Module");

            // 已成功初始化的模块（用于失败回滚）
            var initialized = new List<IModule>();

            foreach (var module in modules)
            {
                if (!module.IsInitialized)
                {
                    try
                    {
                        await module.InitAsync();
                        initialized.Add(module);
                    }
                    catch (Exception ex)
                    {
                        JLogger.LogError($"{Frameworkconst.TagModuleService} 模块 {module.Name} 初始化失败: {ex.Message}");
                        JLogger.LogException(ex);

                        // 回滚已初始化的模块
                        await RollbackAsync(initialized);

                        throw new JulyException(
                            FrameworkErrorCode.ModuleInitFailed,
                            $"模块 {module.Name} 初始化失败，框架启动终止: {ex.Message}",
                            ex);
                    }
                }
            }

            IsInitialized = true;
            JLogger.Log($"{Frameworkconst.TagModuleService} 所有Module初始化完成");
        }

        public async UniTask EnableAllAsync()
        {
            if (!IsInitialized)
            {
                JLogger.LogWarning($"{Frameworkconst.TagModuleService} Module未初始化，无法启用");
                return;
            }

            var modules = GetSnapshot();
            JLogger.Log($"{Frameworkconst.TagModuleService} 开始启用 {modules.Length} 个Module");

            foreach (var module in modules)
            {
                if (module.IsInitialized && !module.IsEnabled)
                {
                    await module.EnableAsync();
                }
            }

            JLogger.Log($"{Frameworkconst.TagModuleService} 所有Module启用完成");
        }

        public async UniTask DisableAllAsync()
        {
            var modules = GetSnapshot();
            JLogger.Log($"{Frameworkconst.TagModuleService} 开始禁用 {modules.Length} 个Module");

            // 逆序禁用
            for (int i = modules.Length - 1; i >= 0; i--)
            {
                if (modules[i].IsEnabled)
                {
                    await modules[i].DisableAsync();
                }
            }

            JLogger.Log($"{Frameworkconst.TagModuleService} 所有Module禁用完成");
        }

        public async UniTask ShutdownAsync()
        {
            if (!IsInitialized)
            {
                return;
            }

            var modules = GetSnapshot();
            JLogger.Log($"{Frameworkconst.TagModuleService} 开始关闭 {modules.Length} 个Module");

            // 逆序关闭
            for (int i = modules.Length - 1; i >= 0; i--)
            {
                if (modules[i].IsInitialized)
                {
                    await modules[i].ShutdownAsync();
                }
            }

            IsInitialized = false;
            JLogger.Log($"{Frameworkconst.TagModuleService} 所有Module已关闭");
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
                    // 记录异常但不中断其他模块
                    JLogger.LogError($"{Frameworkconst.TagModuleService} Module {module.Name} Update异常: {ex.Message}");
                    JLogger.LogException(ex);
                }
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                foreach (var module in _modules)
                {
                    try
                    {
                        module.Dispose();
                    }
                    catch (Exception ex)
                    {
                        JLogger.LogError($"{Frameworkconst.TagModuleService} 释放Module {module.Name} 时异常: {ex.Message}");
                    }
                }

                _modules.Clear();
                _cacheInvalid = true;
            }

            _moduleDic.Clear();
            IsInitialized = false;
            JLogger.Log($"{Frameworkconst.TagModuleService} 已清空所有Module");
        }

        #endregion

        #region 私有方法

        private IModule[] GetSnapshot()
        {
            if (!_cacheInvalid) return _cachedSnapshot;

            lock (_lock)
            {
                _cachedSnapshot = _modules.ToArray();
                _cacheInvalid = false;
            }

            return _cachedSnapshot;
        }

        /// <summary>
        /// 解析模块依赖并按拓扑排序
        /// </summary>
        private List<IModule> ResolveDependencies()
        {
            List<IModule> allModules;
            lock (_lock)
            {
                allModules = _modules.ToList();
            }

            var result = new List<IModule>();
            var visited = new HashSet<Type>();
            var visiting = new HashSet<Type>();

            // 构建依赖图
            var depGraph = new Dictionary<Type, List<Type>>();
            foreach (var module in allModules)
            {
                var type = module.GetType();
                depGraph[type] = new List<Type>();

                if (module is IModuleDependency dep)
                {
                    foreach (var depType in dep.GetDependencies())
                    {
                        if (!_moduleDic.ContainsKey(depType))
                        {
                            throw new JulyException($"模块 {type.Name} 依赖的模块 {depType.Name} 未注册");
                        }
                        depGraph[type].Add(depType);
                    }
                }
            }

            void Visit(Type type)
            {
                if (visited.Contains(type)) return;
                if (visiting.Contains(type))
                {
                    throw new JulyException($"检测到循环依赖，涉及模块: {type.Name}");
                }

                visiting.Add(type);

                if (depGraph.TryGetValue(type, out var deps))
                {
                    foreach (var dep in deps.OrderBy(d => _moduleDic.TryGetValue(d, out var m) ? GetPriority(m) : int.MaxValue))
                    {
                        Visit(dep);
                    }
                }

                visiting.Remove(type);
                visited.Add(type);

                if (_moduleDic.TryGetValue(type, out var module))
                {
                    result.Add(module);
                }
            }

            foreach (var module in allModules.OrderBy(GetPriority))
            {
                Visit(module.GetType());
            }

            return result;
        }

        /// <summary>
        /// 回滚已初始化的模块
        /// </summary>
        private async UniTask RollbackAsync(List<IModule> modules)
        {
            if (modules.Count == 0) return;

            JLogger.LogWarning($"{Frameworkconst.TagModuleService} 开始回滚 {modules.Count} 个已初始化的模块");

            for (int i = modules.Count - 1; i >= 0; i--)
            {
                try
                {
                    var m = modules[i];
                    if (m.IsEnabled) await m.DisableAsync();
                    await m.ShutdownAsync();
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

