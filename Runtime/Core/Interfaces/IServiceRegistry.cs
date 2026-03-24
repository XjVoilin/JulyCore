using System;

namespace JulyCore.Core
{
    public interface IServiceRegistry
    {
        void Register<T>(T instance);
        void Register(Type type, object instance);
        T Resolve<T>();
        object Resolve(Type type);
        bool TryResolve<T>(out T instance);
        void Clear();
    }
}
