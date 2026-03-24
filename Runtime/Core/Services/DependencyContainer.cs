using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace JulyCore.Core
{
    /// <summary>
    /// 依赖注入容器实现
    /// 线程安全，强制显式注册策略，支持构造函数依赖注入
    /// </summary>
    internal class DependencyContainer : IDependencyContainer
    {
        private enum ServiceLifetime
        {
            Singleton,
            Transient
        }

        private class ServiceRegistration
        {
            public ServiceLifetime Lifetime { get; set; }
            public object Instance { get; set; }
            public Func<IDependencyContainer, object> Factory { get; set; }
            public Type ImplementationType { get; set; } // 用于构造函数注入的实现类型
        }

        /// <summary>
        /// 构造函数信息缓存（避免每次反射）
        /// </summary>
        private class ConstructorCache
        {
            public ConstructorInfo Constructor { get; set; }
            public Type[] ParameterTypes { get; set; }
        }

        private readonly ConcurrentDictionary<Type, ServiceRegistration> _registrations = new();
        private readonly ConcurrentDictionary<Type, object> _singletonInstances = new();
        
        /// <summary>
        /// 构造函数缓存：实现类型 -> 构造函数信息
        /// </summary>
        private readonly ConcurrentDictionary<Type, ConstructorCache> _constructorCache = new();

        public void RegisterSingleton<T>(T instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            var interfaceType = typeof(T);
            WarnIfOverride(interfaceType, instance.GetType());

            _registrations[interfaceType] = new ServiceRegistration
            {
                Lifetime = ServiceLifetime.Singleton,
                Instance = instance
            };
            _singletonInstances[interfaceType] = instance;
        }

        public void RegisterSingleton(Type interfaceType, object instance)
        {
            if (interfaceType == null)
                throw new ArgumentNullException(nameof(interfaceType));
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));
            if (!interfaceType.IsInstanceOfType(instance))
                throw new ArgumentException($"实例类型 {instance.GetType().Name} 未实现接口 {interfaceType.Name}");

            WarnIfOverride(interfaceType, instance.GetType());

            _registrations[interfaceType] = new ServiceRegistration
            {
                Lifetime = ServiceLifetime.Singleton,
                Instance = instance
            };
            _singletonInstances[interfaceType] = instance;
        }

        public void RegisterFactory<T>(Func<T> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            var interfaceType = typeof(T);
            WarnIfOverride(interfaceType, null);

            // 包装为接受容器的工厂函数（无依赖的简单工厂）
            _registrations[interfaceType] = new ServiceRegistration
            {
                Lifetime = ServiceLifetime.Transient,
                Factory = container => factory()!
            };
        }

        /// <summary>
        /// 注册瞬态服务（工厂函数可接收容器以解析依赖）
        /// </summary>
        public void RegisterTransient<T>(Func<IDependencyContainer, T> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            var interfaceType = typeof(T);
            WarnIfOverride(interfaceType, null);

            _registrations[interfaceType] = new ServiceRegistration
            {
                Lifetime = ServiceLifetime.Transient,
                Factory = container => factory(container)!
            };
        }

        /// <summary>
        /// 注册瞬态服务（通过构造函数注入自动创建）
        /// </summary>
        public void RegisterTransient<TInterface, TImplementation>() where TImplementation : class, TInterface
        {
            var interfaceType = typeof(TInterface);
            var implementationType = typeof(TImplementation);
            WarnIfOverride(interfaceType, implementationType);

            _registrations[interfaceType] = new ServiceRegistration
            {
                Lifetime = ServiceLifetime.Transient,
                ImplementationType = implementationType
            };
        }

        /// <summary>
        /// 注册单例服务（通过构造函数注入自动创建）
        /// </summary>
        public void RegisterSingleton<TInterface, TImplementation>() where TImplementation : class, TInterface
        {
            var interfaceType = typeof(TInterface);
            var implementationType = typeof(TImplementation);
            WarnIfOverride(interfaceType, implementationType);

            _registrations[interfaceType] = new ServiceRegistration
            {
                Lifetime = ServiceLifetime.Singleton,
                ImplementationType = implementationType
            };
        }

        public void RegisterSingleton(Type interfaceType, Type implementationType)
        {
            if (interfaceType == null) throw new ArgumentNullException(nameof(interfaceType));
            if (implementationType == null) throw new ArgumentNullException(nameof(implementationType));
            if (!interfaceType.IsAssignableFrom(implementationType))
                throw new ArgumentException(
                    $"类型 {implementationType.Name} 未实现接口 {interfaceType.Name}");

            WarnIfOverride(interfaceType, implementationType);

            _registrations[interfaceType] = new ServiceRegistration
            {
                Lifetime = ServiceLifetime.Singleton,
                ImplementationType = implementationType
            };
        }

        public T Resolve<T>()
        {
            var type = typeof(T);
            if (!_registrations.TryGetValue(type, out var registration))
            {
                throw new JulyException($"服务 {type.Name} 未注册");
            }

            return (T)ResolveInternal(type, registration, new HashSet<Type>());
        }

        public object Resolve(Type type)
        {
            if (!_registrations.TryGetValue(type, out var registration))
            {
                throw new JulyException($"服务 {type.Name} 未注册");
            }

            return ResolveInternal(type, registration, new HashSet<Type>());
        }

        public bool TryResolve<T>(out T instance)
        {
            instance = default(T)!;
            var type = typeof(T);

            if (!_registrations.TryGetValue(type, out var registration))
            {
                return false;
            }

            try
            {
                var resolved = ResolveInternal(type, registration, new HashSet<Type>());
                if (resolved is T typedInstance)
                {
                    instance = typedInstance;
                    return true;
                }

                JLogger.LogWarning(
                    $"{Frameworkconst.TagDependencyContainer} TryResolve<{type.Name}> 类型转换失败: {resolved.GetType().Name} -> {typeof(T).Name}");
                return false;
            }
            catch (Exception ex)
            {
                JLogger.LogWarning(
                    $"{Frameworkconst.TagDependencyContainer} TryResolve<{type.Name}> 失败: {ex}");
                return false;
            }
        }

        public bool TryGetExistingSingleton<T>(out T instance)
        {
            instance = default!;
            var type = typeof(T);

            if (!_registrations.TryGetValue(type, out var registration))
                return false;

            if (registration.Lifetime != ServiceLifetime.Singleton || registration.Instance == null)
                return false;

            if (registration.Instance is T typed)
            {
                instance = typed;
                return true;
            }

            return false;
        }

        public bool IsRegistered<T>()
        {
            return _registrations.ContainsKey(typeof(T));
        }

        public void Clear()
        {
            // 倒序释放单例实例
            var instances = _singletonInstances.ToArray();
            for (int i = instances.Length - 1; i >= 0; i--)
            {
                if (instances[i].Value is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception ex)
                    {
                        JLogger.LogException(ex);
                    }
                }
            }

            _registrations.Clear();
            _singletonInstances.Clear();
            _constructorCache.Clear();
        }

        /// <summary>
        /// 内部解析方法，统一处理单例和瞬态服务
        /// </summary>
        private object ResolveInternal(Type serviceType, ServiceRegistration registration, HashSet<Type> resolvingPath)
        {
            // 检查循环依赖
            if (!resolvingPath.Add(serviceType))
            {
                var cycle = string.Join(" -> ", resolvingPath.Append(serviceType).Select(t => t.Name));
                throw new JulyException($"检测到循环依赖: {cycle}");
            }

            try
            {
                if (registration.Lifetime == ServiceLifetime.Singleton)
                {
                    // 单例：如果已有实例，直接返回
                    if (registration.Instance != null)
                    {
                        return registration.Instance;
                    }

                    // 单例：通过构造函数注入创建实例（仅创建一次）
                    if (registration.ImplementationType != null)
                    {
                        var instance = CreateInstanceWithConstructorInjection(registration.ImplementationType, resolvingPath);
                        registration.Instance = instance;
                        _singletonInstances[serviceType] = instance;
                        return instance;
                    }

                    throw new JulyException($"服务 {serviceType.Name} 注册不完整：单例服务必须提供实例或实现类型");
                }
                else
                {
                    // 瞬态：通过工厂创建新实例
                    if (registration.Factory != null)
                    {
                        return registration.Factory(this);
                    }

                    // 瞬态：通过构造函数注入创建新实例
                    if (registration.ImplementationType != null)
                    {
                        return CreateInstanceWithConstructorInjection(registration.ImplementationType, resolvingPath);
                    }

                    throw new JulyException($"服务 {serviceType.Name} 注册不完整：瞬态服务必须提供工厂或实现类型");
                }
            }
            finally
            {
                resolvingPath.Remove(serviceType);
            }
        }

        /// <summary>
        /// 通过构造函数注入创建实例（使用缓存优化反射开销）
        /// </summary>
        private object CreateInstanceWithConstructorInjection(Type implementationType, HashSet<Type> resolvingPath)
        {
            // 获取或创建构造函数缓存
            var cache = _constructorCache.GetOrAdd(implementationType, type =>
            {
                var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
                
                if (constructors.Length == 0)
                {
                    throw new JulyException($"类型 {type.Name} 没有公共构造函数");
                }

                // 优先选择参数最多的构造函数
                var constructor = constructors
                    .OrderByDescending(c => c.GetParameters().Length)
                    .First();

                var parameters = constructor.GetParameters();
                
                return new ConstructorCache
                {
                    Constructor = constructor,
                    ParameterTypes = parameters.Select(p => p.ParameterType).ToArray()
                };
            });

            // 使用缓存的参数类型解析依赖
            var parameterTypes = cache.ParameterTypes;
            var parameterValues = parameterTypes.Length > 0 ? new object[parameterTypes.Length] : Array.Empty<object>();

            for (int i = 0; i < parameterTypes.Length; i++)
            {
                var parameterType = parameterTypes[i];
                
                if (!_registrations.TryGetValue(parameterType, out var paramRegistration))
                {
                    throw new JulyException($"无法解析构造函数参数 {parameterType.Name}，服务未注册");
                }

                parameterValues[i] = ResolveInternal(parameterType, paramRegistration, resolvingPath);
            }

            // 使用缓存的构造函数创建实例
            try
            {
                return cache.Constructor.Invoke(parameterValues);
            }
            catch (Exception ex)
            {
                throw new JulyException($"创建 {implementationType.Name} 实例失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 检查并警告覆盖注册
        /// </summary>
        private void WarnIfOverride(Type interfaceType, Type newImplementationType)
        {
            if (!_registrations.TryGetValue(interfaceType, out var existing))
                return;
            var existingName = existing.Instance?.GetType().Name ?? "Factory";
            var newName = newImplementationType?.Name ?? "Factory";

            JLogger.LogWarning(
                $"{Frameworkconst.TagDependencyContainer} 服务 {interfaceType.Name} 已注册为 {existingName}，将被覆盖为 {newName}");
        }
    }
}