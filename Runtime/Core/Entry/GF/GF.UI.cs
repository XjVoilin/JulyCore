using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Data.UI;
using JulyCore.Module.UI;
using JulyCore.Provider.UI;
using UnityEngine;

namespace JulyCore
{
    public static partial class GF
    {
        /// <summary>
        /// UI相关操作
        /// 业务相关方法（打开、关闭、栈管理）通过 UIModule
        /// 纯技术查询方法（TryGet、IsOpen、Preload）直接调用 IUIProvider
        /// </summary>
        public static class UI
        {
            private static IUIProvider _provider;
            
            private static IUIProvider Provider
            {
                get
                {
                    if (_provider == null)
                    {
                        _context.Container.TryResolve(out _provider);
                    }
                    return _provider;
                }
            }
            
            private static UIModule _module;
            private static UIModule Module
            {
                get
                {
                    _module ??= GetModule<UIModule>();
                    return _module;
                }
            }
            
            #region 窗口配置提供者

            private static IUIWindowConfigProvider _windowConfigProvider;

            /// <summary>
            /// 设置 UI 窗口配置提供者（将 int windowId 映射为 UIOpenOptions）。
            /// 项目侧在热更注册阶段调用。
            /// </summary>
            public static void SetWindowConfig(IUIWindowConfigProvider provider)
            {
                _windowConfigProvider = provider;
            }

            /// <summary>
            /// 通过窗口 ID 打开 UI（从配置提供者查找完整参数）。
            /// </summary>
            public static void Open(int windowId, object data = null,
                CancellationToken cancellationToken = default)
            {
                if (_windowConfigProvider == null)
                {
                    JLogger.LogWarning("[GF.UI] IUIWindowConfigProvider 未设置，请在初始化时调用 SetWindowConfig");
                    return;
                }

                var options = _windowConfigProvider.GetUIOpenOptions(windowId);
                if (options == null) return;

                options.Data = data;
                Open(options, cancellationToken);
            }

            #endregion

            #region 业务层方法（通过 UIModule，有栈管理和事件发布）

            #region 传统 API（返回值 + 异常）

            /// <summary>
            /// 打开UI窗口（同步调用，异步执行）
            /// 如果需要处理异常或获取返回值，请使用OpenAsync()方法
            /// </summary>
            /// <param name="windowId">窗口ID</param>
            /// <param name="windowName">窗口名称</param>
            /// <param name="layer">UI层级</param>
            /// <param name="param">打开参数</param>
            /// <param name="cancellationToken">取消令牌</param>
            public static void Open(int windowId, string windowName, UILayer layer = UILayer.Normal,
                object param = null, CancellationToken cancellationToken = default)
            {
                OpenAsync(windowId, windowName, layer, param, cancellationToken)
                    .Forget(ex => JLogger.LogError($"[GF.UI] 打开UI失败 (ID: {windowId}, Name: {windowName}): {ex.Message}"));
            }

            /// <summary>
            /// 打开UI窗口（同步调用，异步执行）
            /// 如果需要处理异常或获取返回值，请使用OpenAsync()方法
            /// </summary>
            /// <param name="options">打开选项</param>
            /// <param name="cancellationToken">取消令牌</param>
            public static void Open(UIOpenOptions options,
                CancellationToken cancellationToken = default)
            {
                OpenAsync(options, cancellationToken)
                    .Forget(ex => JLogger.LogError($"[GF.UI] 打开UI失败 ({options}): {ex.Message}"));
            }

            /// <summary>
            /// 打开UI窗口
            /// </summary>
            /// <param name="windowId">窗口ID</param>
            /// <param name="windowName">窗口名称（通常为UI类名）</param>
            /// <param name="layer">UI层级</param>
            /// <param name="param">打开参数</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>UI组件实例</returns>
            public static async UniTask<UIBase> OpenAsync(int windowId, string windowName, UILayer layer = UILayer.Normal,
                object param = null, CancellationToken cancellationToken = default)
            {
                var options = new UIOpenOptions
                {
                    WindowIdentifier = new WindowIdentifier(windowId, windowName),
                    Layer = layer,
                    Data = param
                };

                return await Module.OpenAsync(options, cancellationToken);
            }

            /// <summary>
            /// 打开UI窗口（完整配置版）
            /// </summary>
            /// <param name="options">打开选项</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>UI组件实例</returns>
            public static async UniTask<UIBase> OpenAsync(UIOpenOptions options,
                CancellationToken cancellationToken = default)
            {
                return await Module.OpenAsync(options, cancellationToken);
            }

            /// <summary>
            /// 关闭UI窗口（通过ID）
            /// </summary>
            /// <param name="windowId">窗口ID</param>
            /// <param name="destroy">是否销毁（false则隐藏，可再次显示）</param>
            public static void Close(int windowId, bool destroy = false)
            {
                Module.Close(windowId, destroy);
            }

            /// <summary>
            /// 关闭UI窗口（通过WindowIdentifier）
            /// </summary>
            /// <param name="identifier">窗口标识符</param>
            /// <param name="destroy">是否销毁（false则隐藏，可再次显示）</param>
            public static void Close(WindowIdentifier identifier, bool destroy = false)
            {
                Module.Close(identifier, destroy);
            }

            /// <summary>
            /// 关闭UI窗口（通过UIBase实例）
            /// </summary>
            /// <param name="ui">UI实例</param>
            /// <param name="destroy">是否销毁（false则隐藏，可再次显示）</param>
            public static void Close(UIBase ui, bool destroy = false)
            {
                Module.Close(ui, destroy);
            }

            /// <summary>
            /// 关闭UI窗口（异步版本，通过ID）
            /// </summary>
            /// <param name="windowId">窗口ID</param>
            /// <param name="destroy">是否销毁（false则隐藏，可再次显示）</param>
            /// <param name="cancellationToken">取消令牌</param>
            public static async UniTask CloseAsync(int windowId, bool destroy = false,
                CancellationToken cancellationToken = default)
            {
                await Module.CloseAsync(windowId, destroy, cancellationToken);
            }

            /// <summary>
            /// 关闭UI窗口（异步版本，通过WindowIdentifier）
            /// </summary>
            /// <param name="identifier">窗口标识符</param>
            /// <param name="destroy">是否销毁（false则隐藏，可再次显示）</param>
            /// <param name="cancellationToken">取消令牌</param>
            public static async UniTask CloseAsync(WindowIdentifier identifier, bool destroy = false,
                CancellationToken cancellationToken = default)
            {
                await Module.CloseAsync(identifier, destroy, cancellationToken);
            }

            /// <summary>
            /// 关闭UI窗口（异步版本，通过UIBase实例）
            /// </summary>
            /// <param name="ui">UI实例</param>
            /// <param name="destroy">是否销毁（false则隐藏，可再次显示）</param>
            /// <param name="cancellationToken">取消令牌</param>
            public static async UniTask CloseAsync(UIBase ui, bool destroy = false,
                CancellationToken cancellationToken = default)
            {
                await Module.CloseAsync(ui, destroy, cancellationToken);
            }

            /// <summary>
            /// 关闭所有已打开的UI
            /// </summary>
            /// <param name="destroy">是否销毁（false则隐藏）</param>
            public static void CloseAll(bool destroy = false)
            {
                Module.CloseAll(destroy);
            }

            /// <summary>
            /// 关闭指定层级的所有UI
            /// </summary>
            /// <param name="layer">要关闭的UI层级</param>
            /// <param name="destroy">是否销毁（false则隐藏）</param>
            public static void CloseLayer(UILayer layer, bool destroy = false)
            {
                Module.CloseLayer(layer, destroy);
            }

            /// <summary>
            /// 返回上一级UI（栈管理）
            /// </summary>
            /// <returns>是否成功返回</returns>
            public static bool GoBack()
            {
                return Module.GoBack();
            }

            /// <summary>
            /// 获取UI栈深度
            /// </summary>
            /// <returns>栈深度</returns>
            public static int GetStackDepth()
            {
                return Module.StackDepth;
            }

            #endregion

            #region Result 模式 API（推荐）

            /// <summary>
            /// 打开UI窗口（Result 模式）
            /// 
            /// 【优势】
            /// - 不抛出异常，通过 Result 返回错误信息
            /// - 可以获取详细的错误码和错误消息
            /// - 适用于需要优雅处理打开失败的场景
            /// 
            /// 【使用示例】
            /// var result = await GF.UI.TryOpenAsync(options);
            /// if (result.IsSuccess)
            /// {
            ///     var ui = result.Value;
            ///     // 正常使用 UI
            /// }
            /// else
            /// {
            ///     // 根据错误码处理
            ///     switch (result.ErrorCode)
            ///     {
            ///         case FrameworkErrorCode.UINotFound:
            ///             // 提示用户
            ///             break;
            ///         case FrameworkErrorCode.Cancelled:
            ///             // 静默处理
            ///             break;
            ///     }
            /// }
            /// </summary>
            public static async UniTask<FrameworkResult<UIBase>> TryOpenAsync(UIOpenOptions options,
                CancellationToken cancellationToken = default)
            {
                if (options == null)
                {
                    return FrameworkResult<UIBase>.Failure(FrameworkErrorCode.InvalidArgument, "UIOpenOptions不能为空");
                }

                try
                {
                    var ui = await Module.OpenAsync(options, cancellationToken);
                    if (ui == null)
                    {
                        return FrameworkResult<UIBase>.Failure(FrameworkErrorCode.UIOpenFailed, 
                            $"UI打开失败: {options.WindowIdentifier}");
                    }
                    return FrameworkResult<UIBase>.Success(ui);
                }
                catch (OperationCanceledException)
                {
                    return FrameworkResult<UIBase>.Failure(FrameworkErrorCode.Cancelled, "UI打开被取消");
                }
                catch (Exception ex)
                {
                    return FrameworkResult<UIBase>.Failure(FrameworkErrorCode.UIOpenFailed, 
                        $"UI打开失败: {ex.Message}", ex);
                }
            }

            /// <summary>
            /// 打开UI窗口（Result 模式，简化版）
            /// </summary>
            public static async UniTask<FrameworkResult<UIBase>> TryOpenAsync(int windowId, string windowName,
                UILayer layer = UILayer.Normal, object param = null,
                CancellationToken cancellationToken = default)
            {
                var options = new UIOpenOptions
                {
                    WindowIdentifier = new WindowIdentifier(windowId, windowName),
                    Layer = layer,
                    Data = param
                };
                return await TryOpenAsync(options, cancellationToken);
            }

            /// <summary>
            /// 关闭UI窗口（Result 模式，异步版本）
            /// </summary>
            public static async UniTask<FrameworkResult> TryCloseAsync(int windowId, bool destroy = false,
                CancellationToken cancellationToken = default)
            {
                try
                {
                    await Module.CloseAsync(windowId, destroy, cancellationToken);
                    return FrameworkResult.Success();
                }
                catch (OperationCanceledException)
                {
                    return FrameworkResult.Failure(FrameworkErrorCode.Cancelled, "UI关闭被取消");
                }
                catch (Exception ex)
                {
                    return FrameworkResult.Failure(FrameworkErrorCode.Unknown, $"UI关闭失败: {ex.Message}");
                }
            }

            #endregion

            #endregion
            
            #region 技术层方法（直接调用 IUIProvider，纯查询/预加载，无业务逻辑）

            /// <summary>
            /// 获取 UI 专用相机
            /// </summary>
            public static Camera UICamera => Provider?.UICamera;

            /// <summary>
            /// 尝试获取已打开的UI
            /// </summary>
            /// <param name="identifier">窗口标识符</param>
            /// <param name="ui">输出的UI组件实例</param>
            /// <returns>是否获取成功</returns>
            public static bool TryGet(WindowIdentifier identifier, out UIBase ui)
            {
                if (Provider != null)
                {
                    return Provider.TryGet(identifier, out ui);
                }

                ui = null;
                return false;
            }

            /// <summary>
            /// 检查UI是否已打开
            /// </summary>
            /// <param name="identifier">窗口标识符</param>
            /// <returns>是否已打开</returns>
            public static bool IsOpen(WindowIdentifier identifier)
            {
                return Provider != null && Provider.IsOpen(identifier);
            }

            /// <summary>
            /// 预加载UI（提前加载资源，不实例化）
            /// </summary>
            /// <param name="uiType">UI组件类型</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>是否预加载成功</returns>
            public static UniTask<bool> PreloadAsync(Type uiType, CancellationToken cancellationToken = default)
            {
                if (Provider == null)
                {
                    JLogger.LogWarning("[GF.UI] IUIProvider未注册");
                    return UniTask.FromResult(false);
                }
                return Provider.PreloadAsync(uiType, cancellationToken);
            }

            /// <summary>
            /// 预加载多个UI
            /// </summary>
            /// <param name="uiTypes">UI类型列表</param>
            /// <param name="cancellationToken">取消令牌</param>
            public static UniTask PreloadBatchAsync(Type[] uiTypes, CancellationToken cancellationToken = default)
            {
                if (Provider == null)
                {
                    JLogger.LogWarning("[GF.UI] IUIProvider未注册");
                    return UniTask.CompletedTask;
                }
                return Provider.PreloadBatchAsync(uiTypes, cancellationToken);
            }

            /// <summary>
            /// 释放预加载的UI资源
            /// </summary>
            /// <param name="uiType">UI组件类型</param>
            public static void ReleasePreload(Type uiType)
            {
                Provider?.ReleasePreload(uiType);
            }

            /// <summary>
            /// 检查UI是否已预加载
            /// </summary>
            /// <param name="uiType">UI组件类param>
            /// <returns>是否已预加载</returns>
            public static bool IsPreloaded(Type uiType)
            {
                return Provider != null && Provider.IsPreloaded(uiType);
            }
            
            #endregion

            #region Tip

            /// <summary>
            /// 显示 Tip 提示（指定时长）
            /// </summary>
            /// <param name="message">提示内容</param>
            /// <param name="duration">显示时长（秒）</param>
            public static void ShowTip(string message, float duration = 0)
            {
                Provider?.ShowTip(message, duration);
            }
            
            #endregion
            
            /// <summary>
            /// 重置缓存的 Provider 引用（框架重启时调用）
            /// </summary>
            internal static void Reset()
            {
                _provider = null;
            }
        }
    }
}
