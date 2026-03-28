using System;
using System.Collections.Generic;

namespace JulyCore.Core
{
    internal class ServiceRegistry : IServiceRegistry
    {
        private readonly Dictionary<Type, object> _services = new();

        /// <summary>
        /// 注册服务实例。同键重复注册会覆盖（打 Warning）。
        /// 设计意图：Provider 可替换是核心需求（平台切换、AOT/热更覆盖），
        /// 因此 Registry 允许覆盖而非抛异常。
        /// </summary>
        public void Register<T>(T instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            var type = typeof(T);
            WarnIfOverride(type, instance.GetType());
            _services[type] = instance;
        }

        public void Register(Type type, object instance)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (!type.IsInstanceOfType(instance))
                throw new ArgumentException($"实例类型 {instance.GetType().Name} 未实现 {type.Name}");
            WarnIfOverride(type, instance.GetType());
            _services[type] = instance;
        }

        public T Resolve<T>()
        {
            var type = typeof(T);
            if (!_services.TryGetValue(type, out var instance))
                throw new JulyException($"服务 {type.Name} 未注册");
            return (T)instance;
        }

        public object Resolve(Type type)
        {
            if (!_services.TryGetValue(type, out var instance))
                throw new JulyException($"服务 {type.Name} 未注册");
            return instance;
        }

        public bool TryResolve<T>(out T instance)
        {
            instance = default;
            if (!_services.TryGetValue(typeof(T), out var obj))
                return false;
            if (obj is T typed)
            {
                instance = typed;
                return true;
            }
            return false;
        }

        public void Clear()
        {
            var snapshot = new List<object>(_services.Values);
            for (int i = snapshot.Count - 1; i >= 0; i--)
            {
                if (snapshot[i] is IDisposable disposable)
                {
                    try { disposable.Dispose(); }
                    catch (Exception ex) { JLogger.LogException(ex); }
                }
            }
            _services.Clear();
        }

        private void WarnIfOverride(Type type, Type newImplType)
        {
            if (!_services.TryGetValue(type, out var existing)) return;
            JLogger.LogWarning(
                $"{Frameworkconst.TagDependencyContainer} 服务 {type.Name} 已注册为 {existing.GetType().Name}，将被覆盖为 {newImplType?.Name}");
        }
    }
}
