using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Data.HotUpdate;
using JulyCore.Module.HotUpdate;

namespace JulyCore
{
    public static partial class GF
    {
        /// <summary>
        /// 热更新相关操作
        /// </summary>
        public static class HotUpdate
        {
            private static HotUpdateModule _module;
            private static HotUpdateModule Module
            {
                get
                {
                    _module ??= GetModule<HotUpdateModule>();
                    return _module;
                }
            }
            
            #region 状态查询

            /// <summary>
            /// 获取当前热更新状态
            /// </summary>
            public static HotUpdateState State
            {
                get
                {
                    return Module.State;
                }
            }

            /// <summary>
            /// 是否已加载热更新
            /// </summary>
            public static bool IsLoaded => Module.IsLoaded;

            /// <summary>
            /// 获取已加载的程序集列表
            /// </summary>
            public static IReadOnlyList<Assembly> LoadedAssemblies => Module.LoadedAssemblies ?? Array.Empty<Assembly>();

            /// <summary>
            /// 获取最后一次加载结果
            /// </summary>
            public static HotUpdateResult LastResult => Module.LastResult;

            #endregion

            #region 加载操作

            /// <summary>
            /// 使用配置加载热更新
            /// </summary>
            /// <param name="config">热更新配置</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>加载结果</returns>
            public static async UniTask<HotUpdateResult> LoadAsync(
                HotUpdateConfig config,
                CancellationToken cancellationToken = default)
            {
                return await Module.LoadAsync(config, cancellationToken);
            }

            /// <summary>
            /// 快速加载热更新（使用默认配置）
            /// </summary>
            /// <param name="hotUpdateAssemblies">热更新程序集名称列表</param>
            /// <param name="aotMetaAssemblies">AOT元数据程序集名称列表（可选）</param>
            /// <param name="entryClass">入口类全名（可选）</param>
            /// <param name="entryMethod">入口方法名（默认"Start"）</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>加载结果</returns>
            public static async UniTask<HotUpdateResult> LoadAsync(
                List<string> hotUpdateAssemblies,
                List<string> aotMetaAssemblies = null,
                string entryClass = null,
                string entryMethod = "Start",
                CancellationToken cancellationToken = default)
            {
                return await Module.LoadAsync(hotUpdateAssemblies, aotMetaAssemblies, entryClass, entryMethod,
                    cancellationToken);
            }

            /// <summary>
            /// 同步方式加载热更新（Fire and Forget）
            /// </summary>
            /// <param name="config">热更新配置</param>
            /// <param name="onComplete">完成回调</param>
            public static void Load(HotUpdateConfig config, Action<HotUpdateResult> onComplete = null)
            {
                LoadAsync(config).ContinueWith(result => { onComplete?.Invoke(result); }).Forget();
            }

            #endregion

            #region 类型操作

            /// <summary>
            /// 从热更新程序集获取类型
            /// </summary>
            /// <param name="typeFullName">类型全名</param>
            /// <returns>类型，未找到返回null</returns>
            public static Type GetType(string typeFullName)
            {
                return Module.GetType(typeFullName);
            }

            /// <summary>
            /// 从热更新程序集获取所有符合条件的类型
            /// </summary>
            /// <param name="predicate">筛选条件</param>
            /// <returns>类型列表</returns>
            public static List<Type> GetTypes(Func<Type, bool> predicate = null)
            {
                return Module.GetTypes(predicate) ?? new List<Type>();
            }

            /// <summary>
            /// 获取所有实现指定接口的热更新类型
            /// </summary>
            /// <typeparam name="T">接口类型</typeparam>
            /// <returns>类型列表</returns>
            public static List<Type> GetTypesImplementing<T>()
            {
                return GetTypes(t => typeof(T).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
            }

            /// <summary>
            /// 获取所有带有指定特性的热更新类型
            /// </summary>
            /// <typeparam name="TAttribute">特性类型</typeparam>
            /// <returns>类型列表</returns>
            public static List<Type> GetTypesWithAttribute<TAttribute>() where TAttribute : Attribute
            {
                return GetTypes(t => t.GetCustomAttribute<TAttribute>() != null);
            }

            #endregion

            #region 实例创建

            /// <summary>
            /// 创建热更新类型实例
            /// </summary>
            /// <typeparam name="T">基类或接口类型</typeparam>
            /// <param name="typeFullName">类型全名</param>
            /// <param name="args">构造函数参数</param>
            /// <returns>实例</returns>
            public static T CreateInstance<T>(string typeFullName, params object[] args) where T : class
            {
                return Module.CreateInstance<T>(typeFullName, args);
            }

            /// <summary>
            /// 创建热更新类型实例（无泛型约束版本）
            /// </summary>
            /// <param name="typeFullName">类型全名</param>
            /// <param name="args">构造函数参数</param>
            /// <returns>实例</returns>
            public static object CreateInstance(string typeFullName, params object[] args)
            {
                var type = GetType(typeFullName);
                if (type == null)
                {
                    JLogger.LogWarning($"[GF.HotUpdate] 未找到类型: {typeFullName}");
                    return null;
                }

                try
                {
                    return Activator.CreateInstance(type, args);
                }
                catch (Exception ex)
                {
                    JLogger.LogError($"[GF.HotUpdate] 创建实例失败: {typeFullName}, 错误: {ex.Message}");
                    return null;
                }
            }

            #endregion

            #region 入口执行

            /// <summary>
            /// 执行热更新入口方法
            /// </summary>
            /// <param name="entryClassName">入口类全名</param>
            /// <param name="entryMethodName">入口方法名</param>
            /// <param name="parameters">方法参数</param>
            /// <returns>是否执行成功</returns>
            public static bool ExecuteEntry(string entryClassName, string entryMethodName, object[] parameters = null)
            {
                return Module.ExecuteEntry(entryClassName, entryMethodName, parameters);
            }

            /// <summary>
            /// 调用热更新类型的静态方法
            /// </summary>
            /// <param name="typeFullName">类型全名</param>
            /// <param name="methodName">方法名</param>
            /// <param name="parameters">方法参数</param>
            /// <returns>返回值</returns>
            public static object InvokeStaticMethod(string typeFullName, string methodName, params object[] parameters)
            {
                var type = GetType(typeFullName);
                if (type == null)
                {
                    JLogger.LogWarning($"[GF.HotUpdate] 未找到类型: {typeFullName}");
                    return null;
                }

                try
                {
                    var method = type.GetMethod(methodName,
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (method == null)
                    {
                        JLogger.LogWarning($"[GF.HotUpdate] 未找到方法: {typeFullName}.{methodName}");
                        return null;
                    }

                    return method.Invoke(null, parameters);
                }
                catch (Exception ex)
                {
                    JLogger.LogError($"[GF.HotUpdate] 调用方法失败: {typeFullName}.{methodName}, 错误: {ex.Message}");
                    return null;
                }
            }

            #endregion

            #region 事件订阅辅助

            /// <summary>
            /// 订阅热更新状态变化事件
            /// </summary>
            /// <param name="handler">事件处理器</param>
            /// <param name="target">绑定对象（用于批量解绑）</param>
            public static void OnStateChanged(Action<HotUpdateStateChangedEvent> handler, object target)
            {
                _context.EventBus.Subscribe(handler, target);
            }

            /// <summary>
            /// 订阅热更新进度事件
            /// </summary>
            /// <param name="handler">事件处理器</param>
            /// <param name="target">绑定对象（用于批量解绑）</param>
            public static void OnProgress(Action<HotUpdateProgressEvent> handler, object target)
            {
                _context.EventBus.Subscribe(handler, target);
            }

            /// <summary>
            /// 取消订阅热更新状态变化事件
            /// </summary>
            /// <param name="handler">事件处理器</param>
            public static void OffStateChanged(Action<HotUpdateStateChangedEvent> handler)
            {
                _context.EventBus.Unsubscribe(handler);
            }

            /// <summary>
            /// 取消订阅热更新进度事件
            /// </summary>
            /// <param name="handler">事件处理器</param>
            public static void OffProgress(Action<HotUpdateProgressEvent> handler)
            {
                _context.EventBus.Unsubscribe(handler);
            }

            #endregion
        }
    }
}