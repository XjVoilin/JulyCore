using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Data.UI;
using UnityEngine;

namespace JulyCore.Provider.UI
{
    /// <summary>
    /// UI资源路径解析器接口
    /// 用于将WindowName映射到UI类型和资源路径
    /// </summary>
    public interface IUIResourcePathResolver
    {
        /// <summary>
        /// 通过WindowName获取UI类型
        /// </summary>
        /// <param name="windowName">窗口名称</param>
        /// <returns>UI类型，如果未找到则返回null</returns>
        Type GetUIType(string windowName);

        /// <summary>
        /// 获取UI类型的资源路径
        /// </summary>
        /// <param name="uiType">UI类型</param>
        /// <returns>资源路径</returns>
        string GetResourcePath(Type uiType);
    }

    /// <summary>
    /// 默认UI资源路径解析器
    /// 使用约定：WindowName就是TypeName，资源路径为 "{TypeName}"
    /// </summary>
    public class DefaultUIResourcePathResolver : IUIResourcePathResolver
    {
        /// <summary>
        /// WindowName到Type的映射缓存（性能优化）
        /// </summary>
        private readonly Dictionary<string, Type> _windowNameToTypeCache = new Dictionary<string, Type>();

        /// <summary>
        /// 通过WindowName获取UI类型
        /// 约定：WindowName就是TypeName，通过反射查找类型
        /// </summary>
        public Type GetUIType(string windowName)
        {
            if (string.IsNullOrEmpty(windowName))
            {
                return null;
            }

            // 先从缓存查找
            if (_windowNameToTypeCache.TryGetValue(windowName, out var cachedType))
            {
                return cachedType;
            }

            // 通过反射查找类型（在所有已加载的程序集中查找）
            // 约定：WindowName就是TypeName
            var uiType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly =>
                {
                    try
                    {
                        return assembly.GetTypes();
                    }
                    catch
                    {
                        return Enumerable.Empty<Type>();
                    }
                })
                .FirstOrDefault(type =>
                    type.Name == windowName &&
                    typeof(UIBase).IsAssignableFrom(type) &&
                    !type.IsAbstract);

            if (uiType != null)
            {
                _windowNameToTypeCache[windowName] = uiType;
            }

            return uiType;
        }

        /// <summary>
        /// 获取UI类型的资源路径
        /// </summary>
        public string GetResourcePath(Type uiType)
        {
            if (uiType == null)
            {
                throw new ArgumentNullException(nameof(uiType));
            }

            return $"{uiType.Name}";
        }
    }

    /// <summary>
    /// UI提供者接口
    /// 纯技术执行层：负责资源加载、UI实例化、GameObject操作
    /// 不包含任何业务语义，不维护业务状态
    /// 所有业务逻辑（栈管理、层级管理）由Module层处理
    /// </summary>
    public interface IUIProvider : Core.IProvider
    {
        /// <summary>
        /// UI 专用相机
        /// </summary>
        Camera UICamera { get; }

        /// <summary>
        /// 打开UI（异步加载并实例化）
        /// 通过WindowIdentifier中的WindowName自动解析UI类型
        /// </summary>
        /// <param name="options">打开选项（必须包含WindowIdentifier）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>UI组件实例</returns>
        UniTask<UIBase> OpenAsync(UIOpenOptions options, CancellationToken cancellationToken = default);

        /// <summary>
        /// 关闭UI（通过WindowIdentifier，同步版本，不等待动画）
        /// </summary>
        /// <param name="identifier">窗口标识符</param>
        /// <param name="destroy">是否销毁（false则隐藏）</param>
        void Close(WindowIdentifier identifier, bool destroy = false, UIAnimationType? animationType = null);

        /// <summary>
        /// 关闭UI（通过WindowIdentifier，异步版本，等待动画完成）
        /// </summary>
        /// <param name="identifier">窗口标识符</param>
        /// <param name="destroy">是否销毁（false则隐藏）</param>
        /// <param name="cancellationToken">取消令牌</param>
        UniTask CloseAsync(WindowIdentifier identifier, bool destroy = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// 尝试获取已打开的UI
        /// </summary>
        /// <param name="identifier">窗口标识符</param>
        /// <param name="ui">输出的UI组件实例</param>
        /// <returns>是否获取成功</returns>
        bool TryGet(WindowIdentifier identifier, out UIBase ui);

        /// <summary>
        /// 检查UI是否已打开
        /// </summary>
        /// <param name="identifier">窗口标识符</param>
        /// <returns>是否已打开</returns>
        bool IsOpen(WindowIdentifier identifier);

        /// <summary>
        /// 获取UI信息
        /// </summary>
        /// <param name="identifier">窗口标识符</param>
        /// <param name="uiInfo">输出的UI信息</param>
        /// <returns>是否获取成功</returns>
        bool TryGetUIInfo(WindowIdentifier identifier, out UIInfo uiInfo);

        /// <summary>
        /// 显示UI（设置UI可见并调用Open方法）
        /// </summary>
        /// <param name="identifier">窗口标识符</param>
        void ShowUI(WindowIdentifier identifier);

        #region 预加载

        /// <summary>
        /// 预加载UI（提前加载资源，不实例化）
        /// </summary>
        /// <param name="uiType">UI组件类型</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否预加载成功</returns>
        UniTask<bool> PreloadAsync(Type uiType, CancellationToken cancellationToken = default);

        /// <summary>
        /// 预加载多个UI
        /// </summary>
        /// <param name="uiTypes">UI类型列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        UniTask PreloadBatchAsync(Type[] uiTypes, CancellationToken cancellationToken = default);

        /// <summary>
        /// 释放预加载的UI资源
        /// </summary>
        /// <param name="uiType">UI组件类型</param>
        void ReleasePreload(Type uiType);

        /// <summary>
        /// 检查UI是否已预加载
        /// </summary>
        /// <param name="uiType">UI组件类型</param>
        /// <returns>是否已预加载</returns>
        bool IsPreloaded(Type uiType);

        #endregion

        #region Tip

        /// <summary>
        /// 显示 Tip 提示
        /// </summary>
        /// <param name="message">提示内容</param>
        /// <param name="duration">显示时长（秒），0 则使用默认值</param>
        void ShowTip(string message, float duration = 0);

        #endregion
    }
}
