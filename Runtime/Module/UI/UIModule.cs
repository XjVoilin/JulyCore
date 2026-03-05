using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Data.UI;
using JulyCore.Module.Base;
using JulyCore.Provider.UI;
using JulyCore.Provider.UI.Events;

namespace JulyCore.Module.UI
{
    /// <summary>
    /// UI模块
    /// 
    /// 【职责】
    /// - 业务语义与流程调度：UI栈管理、层级管理、打开/关闭规则
    /// - 状态变化通知：通过 EventBus 发布 UI 状态事件
    /// - 业务层 ID 映射：维护 ID -> WindowIdentifier 的映射
    /// 
    /// 【通信模式】
    /// - 调用 Provider：执行技术操作（资源加载、实例化、动画等）
    /// - 发布 Event：通知外部 UI 状态变化（供其他模块或业务层订阅）
    /// </summary>
    internal class UIModule : ModuleBase
    {
        private IUIProvider _uiProvider;

        protected override LogChannel LogChannel => LogChannel.UI;

        // 业务状态：UI栈（用于返回功能）
        private readonly LinkedList<WindowIdentifier> _uiStack = new LinkedList<WindowIdentifier>();

        // 业务层映射：ID -> WindowIdentifier（用于通过 ID 查找）
        private readonly Dictionary<int, WindowIdentifier> _idToIdentifier = new Dictionary<int, WindowIdentifier>();

        // 业务层映射：已打开的 UI 信息（用于 CloseAll, CloseLayer 等批量操作）
        private readonly Dictionary<WindowIdentifier, UILayer> _openedUILayers = new Dictionary<WindowIdentifier, UILayer>();

        public override int Priority => Frameworkconst.PriorityUIModule;

        protected override UniTask OnInitAsync()
        {
            try
            {
                _uiProvider = GetProvider<IUIProvider>();
                if (_uiProvider == null)
                {
                    throw new JulyException($"[{Name}] 未找到IUIProvider，请先注册UIProvider");
                }

                Log($"[{Name}] UI模块初始化完成");
                return base.OnInitAsync();
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] UI模块初始化失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 打开UI（业务层：处理栈管理、层级管理等业务规则）
        /// </summary>
        internal async UniTask<UIBase> OpenAsync(UIOpenOptions options, CancellationToken cancellationToken = default)
        {
            EnsureProvider();

            // 通过Provider打开UI（技术层）
            var ui = await _uiProvider.OpenAsync(options, cancellationToken);
            if (ui == null)
            {
                return null;
            }

            var identifier = options.WindowIdentifier;

            // 业务层：记录 ID 映射
            _idToIdentifier[identifier.ID] = identifier;
            _openedUILayers[identifier] = options.Layer;

            // 业务规则：如果AddToStack为true，加入栈
            if (options.AddToStack)
            {
                AddToStack(identifier);
            }

            // 通知外部：UI已打开
            PublishOpenEvent(identifier, options.Layer, options.Data);

            return ui;
        }

        /// <summary>
        /// 关闭UI（通过ID，业务层：处理栈管理）
        /// </summary>
        internal void Close(int id, bool destroy = false)
        {
            if (!_idToIdentifier.TryGetValue(id, out var identifier))
            {
                LogWarning($"[{Name}] 未找到ID为 {id} 的UI");
                return;
            }

            Close(identifier, destroy);
        }

        /// <summary>
        /// 关闭UI（通过WindowIdentifier，业务层：处理栈管理）
        /// </summary>
        internal void Close(WindowIdentifier identifier, bool destroy = false)
        {
            EnsureProvider();

            // 获取层级信息（用于事件发布）
            var layer = _openedUILayers.GetValueOrDefault(identifier, UILayer.Normal);

            // 业务规则：从栈中移除
            RemoveFromStack(identifier);

            // 业务层：移除映射
            _idToIdentifier.Remove(identifier.ID);
            _openedUILayers.Remove(identifier);

            // 执行操作：通过Provider关闭UI
            _uiProvider.Close(identifier, destroy);

            // 通知外部：UI已关闭
            PublishCloseEvent(identifier, layer, destroy);
        }

        /// <summary>
        /// 关闭UI（通过UIBase实例，业务层：处理栈管理）
        /// </summary>
        internal void Close(UIBase ui, bool destroy = false)
        {
            if (ui == null) return;

            // 通过遍历查找对应的 WindowIdentifier
            var identifier = FindIdentifierByInstanceId(ui.GetInstanceID());
            if (identifier != null)
            {
                Close(identifier, destroy);
            }
            else
            {
                LogWarning($"[{Name}] 未找到UI实例 {ui.name} 对应的WindowIdentifier");
            }
        }

        /// <summary>
        /// 关闭UI（通过ID，异步版本）
        /// </summary>
        internal async UniTask CloseAsync(int id, bool destroy = false, CancellationToken cancellationToken = default)
        {
            if (!_idToIdentifier.TryGetValue(id, out var identifier))
            {
                LogWarning($"[{Name}] 未找到ID为 {id} 的UI");
                return;
            }

            await CloseAsync(identifier, destroy, cancellationToken);
        }

        /// <summary>
        /// 关闭UI（通过WindowIdentifier，异步版本）
        /// </summary>
        internal async UniTask CloseAsync(WindowIdentifier identifier, bool destroy = false, CancellationToken cancellationToken = default)
        {
            EnsureProvider();

            // 获取层级信息（用于事件发布）
            var layer = _openedUILayers.GetValueOrDefault(identifier, UILayer.Normal);

            // 业务规则：从栈中移除
            RemoveFromStack(identifier);

            // 业务层：移除映射
            _idToIdentifier.Remove(identifier.ID);
            _openedUILayers.Remove(identifier);

            // 执行操作：通过Provider关闭UI（等待动画完成）
            await _uiProvider.CloseAsync(identifier, destroy, cancellationToken);

            // 通知外部：UI已关闭
            PublishCloseEvent(identifier, layer, destroy);
        }

        /// <summary>
        /// 关闭UI（通过UIBase实例，异步版本）
        /// </summary>
        internal async UniTask CloseAsync(UIBase ui, bool destroy = false, CancellationToken cancellationToken = default)
        {
            if (ui == null) return;

            var identifier = FindIdentifierByInstanceId(ui.GetInstanceID());
            if (identifier != null)
            {
                await CloseAsync(identifier, destroy, cancellationToken);
            }
            else
            {
                LogWarning($"[{Name}] 未找到UI实例 {ui.name} 对应的WindowIdentifier");
            }
        }

        /// <summary>
        /// 关闭所有UI（业务层：清空栈）
        /// </summary>
        internal void CloseAll(bool destroy = false)
        {
            EnsureProvider();

            // 复制列表以避免在遍历时修改集合
            var identifiersToClose = _openedUILayers.Keys.ToList();

            // 业务规则：清空栈和映射
            _uiStack.Clear();
            _idToIdentifier.Clear();
            _openedUILayers.Clear();

            // 通过Provider关闭所有UI
            foreach (var identifier in identifiersToClose)
            {
                _uiProvider.Close(identifier, destroy);
            }
        }

        /// <summary>
        /// 关闭指定层级的所有UI（业务规则：层级管理）
        /// </summary>
        internal void CloseLayer(UILayer layer, bool destroy = false)
        {
            EnsureProvider();

            // 查找指定层级的 UI
            var identifiersToClose = _openedUILayers
                .Where(kvp => kvp.Value == layer)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var identifier in identifiersToClose)
            {
                Close(identifier, destroy);
            }
        }

        /// <summary>
        /// 获取指定层级已打开的UI数量
        /// </summary>
        internal int GetLayerUICount(UILayer layer)
        {
            return _openedUILayers.Count(kvp => kvp.Value == layer);
        }

        /// <summary>
        /// 返回上一级UI（业务规则：栈管理）
        /// </summary>
        internal bool GoBack()
        {
            EnsureProvider();

            if (_uiStack.Count == 0)
            {
                return false;
            }

            // 关闭当前UI（从栈尾移除）
            var lastNode = _uiStack.Last;
            if (lastNode == null)
            {
                return false;
            }

            var currentIdentifier = lastNode.Value;
            _uiStack.RemoveLast();

            // 获取层级信息
            var layer = _openedUILayers.GetValueOrDefault(currentIdentifier, UILayer.Normal);

            // 移除映射
            _idToIdentifier.Remove(currentIdentifier.ID);
            _openedUILayers.Remove(currentIdentifier);

            // 执行操作：通过Provider关闭当前UI
            _uiProvider.Close(currentIdentifier, destroy: false);

            // 通知外部：UI已关闭
            PublishCloseEvent(currentIdentifier, layer, false);

            // 显示上一个UI
            if (_uiStack.Count > 0)
            {
                var previousIdentifier = _uiStack.Last.Value;
                _uiProvider.ShowUI(previousIdentifier);
                Log($"[{Name}] 返回UI: {previousIdentifier.WindowName}");
            }

            return true;
        }

        /// <summary>
        /// 获取UI栈深度
        /// </summary>
        internal int StackDepth => _uiStack.Count;

        #region 私有辅助方法

        /// <summary>
        /// 通过 InstanceID 查找 WindowIdentifier
        /// </summary>
        private WindowIdentifier FindIdentifierByInstanceId(int instanceId)
        {
            // 遍历查找匹配的 UI
            foreach (var identifier in _openedUILayers.Keys)
            {
                if (_uiProvider.TryGet(identifier, out var ui) && ui != null && ui.GetInstanceID() == instanceId)
                {
                    return identifier;
                }
            }
            return null;
        }

        /// <summary>
        /// 将UI加入栈
        /// </summary>
        private void AddToStack(WindowIdentifier identifier)
        {
            if (identifier == null) return;

            // 如果已在栈中，先移除（避免重复）
            RemoveFromStack(identifier);
            _uiStack.AddLast(identifier);
        }

        /// <summary>
        /// 从栈中移除UI
        /// </summary>
        private void RemoveFromStack(WindowIdentifier identifier)
        {
            if (identifier == null || _uiStack.Count == 0) return;

            var node = _uiStack.First;
            while (node != null)
            {
                var next = node.Next;
                if (node.Value == identifier)
                {
                    _uiStack.Remove(node);
                    break;
                }
                node = next;
            }
        }

        private void EnsureProvider()
        {
            if (_uiProvider == null)
            {
                throw new InvalidOperationException($"[{Name}] UIProvider未初始化");
            }
        }

        #endregion

        #region 事件通知

        private void PublishOpenEvent(WindowIdentifier identifier, UILayer layer, object param)
        {
            try
            {
                EventBus.Publish(new UIOpenEvent
                {
                    Identifier = identifier,
                    Layer = layer,
                    Param = param
                });
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 发布UI打开事件失败: {ex.Message}");
            }
        }

        private void PublishCloseEvent(WindowIdentifier identifier, UILayer layer, bool isDestroyed)
        {
            try
            {
                EventBus.Publish(new UICloseEvent
                {
                    Identifier = identifier,
                    Layer = layer,
                    IsDestroyed = isDestroyed
                });
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 发布UI关闭事件失败: {ex.Message}");
            }
        }

        #endregion

        protected override UniTask OnShutdownAsync()
        {
            CloseAll(destroy: true);
            return base.OnShutdownAsync();
        }
    }
}
