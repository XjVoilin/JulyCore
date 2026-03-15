using System;

namespace JulyCore.Core
{
    /// <summary>
    /// 依赖注入容器接口
    /// 提供服务的注册和解析功能
    /// </summary>
    public interface IDependencyContainer
    {
        /// <summary>
        /// 注册单例服务（使用实例）
        /// </summary>
        /// <typeparam name="T">接口类型</typeparam>
        /// <param name="instance">服务实例</param>
        void RegisterSingleton<T>(T instance);

        /// <summary>
        /// 注册单例服务（非泛型版本，用于运行时动态注册）
        /// </summary>
        /// <param name="interfaceType">接口类型</param>
        /// <param name="instance">服务实例</param>
        void RegisterSingleton(Type interfaceType, object instance);

        /// <summary>
        /// 注册工厂方法
        /// </summary>
        /// <typeparam name="T">接口类型</typeparam>
        /// <param name="factory">工厂方法</param>
        void RegisterFactory<T>(Func<T> factory);

        /// <summary>
        /// 注册瞬态服务（通过构造函数注入自动创建）
        /// </summary>
        /// <typeparam name="TInterface">接口类型</typeparam>
        /// <typeparam name="TImplementation">实现类型</typeparam>
        void RegisterTransient<TInterface, TImplementation>() where TImplementation : class, TInterface;

        /// <summary>
        /// 注册单例服务（通过构造函数注入自动创建）
        /// </summary>
        /// <typeparam name="TInterface">接口类型</typeparam>
        /// <typeparam name="TImplementation">实现类型</typeparam>
        void RegisterSingleton<TInterface, TImplementation>() where TImplementation : class, TInterface;

        /// <summary>
        /// 注册单例服务（非泛型版本，用于运行时动态注册实现类型）
        /// </summary>
        /// <param name="interfaceType">接口类型</param>
        /// <param name="implementationType">实现类型（将通过构造函数注入自动创建）</param>
        void RegisterSingleton(Type interfaceType, Type implementationType);

        /// <summary>
        /// 解析服务
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        /// <returns>服务实例</returns>
        T Resolve<T>();

        /// <summary>
        /// 解析服务（非泛型版本）
        /// </summary>
        /// <param name="type">服务类型</param>
        /// <returns>服务实例</returns>
        object Resolve(Type type);

        /// <summary>
        /// 尝试解析服务
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        /// <param name="instance">输出的服务实例</param>
        /// <returns>是否解析成功</returns>
        bool TryResolve<T>(out T instance);

        /// <summary>
        /// 尝试获取已实例化的单例（不触发懒创建）。
        /// 仅当单例已被 Resolve 过或以实例注册时返回 true。
        /// </summary>
        bool TryGetExistingSingleton<T>(out T instance);

        /// <summary>
        /// 检查服务是否已注册
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        /// <returns>是否已注册</returns>
        bool IsRegistered<T>();

        /// <summary>
        /// 清除所有注册
        /// </summary>
        void Clear();
    }
}

