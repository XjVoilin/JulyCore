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

        internal void Close(int id, bool destroy = true)
        {
            if (!_idToIdentifier.TryGetValue(id, out var identifier))
            {
                LogWarning($"[{Name}] 未找到ID为 {id} 的UI");
                return;
            }

            Close(identifier);
        }

        internal void Close(WindowIdentifier identifier, bool destroy = true)
        {
            EnsureProvider();

            var layer = _openedUILayers.GetValueOrDefault(identifier, UILayer.Normal);

            RemoveFromStack(identifier);
            _idToIdentifier.Remove(identifier.ID);
            _openedUILayers.Remove(identifier);

            _uiProvider.Close(identifier);
            PublishCloseEvent(identifier, layer);
        }

        internal void Close(UIBase ui, bool destroy = true)
        {
            if (ui == null) return;

            var identifier = FindIdentifierByInstanceId(ui.GetInstanceID());
            if (identifier != null)
            {
                Close(identifier);
            }
            else
            {
                LogWarning($"[{Name}] 未找到UI实例 {ui.name} 对应的WindowIdentifier");
            }
        }

        internal async UniTask CloseAsync(int id, bool destroy = true, CancellationToken cancellationToken = default)
        {
            if (!_idToIdentifier.TryGetValue(id, out var identifier))
            {
                LogWarning($"[{Name}] 未找到ID为 {id} 的UI");
                return;
            }

            await CloseAsync(identifier, true, cancellationToken);
        }

        internal async UniTask CloseAsync(WindowIdentifier identifier, bool destroy = true, CancellationToken cancellationToken = default)
        {
            EnsureProvider();

            var layer = _openedUILayers.GetValueOrDefault(identifier, UILayer.Normal);

            RemoveFromStack(identifier);
            _idToIdentifier.Remove(identifier.ID);
            _openedUILayers.Remove(identifier);

            await _uiProvider.CloseAsync(identifier, true, cancellationToken);
            PublishCloseEvent(identifier, layer);
        }

        internal async UniTask CloseAsync(UIBase ui, bool destroy = true, CancellationToken cancellationToken = default)
        {
            if (ui == null) return;

            var identifier = FindIdentifierByInstanceId(ui.GetInstanceID());
            if (identifier != null)
            {
                await CloseAsync(identifier, true, cancellationToken);
            }
            else
            {
                LogWarning($"[{Name}] 未找到UI实例 {ui.name} 对应的WindowIdentifier");
            }
        }

        internal void CloseAll(bool destroy = true)
        {
            EnsureProvider();

            var identifiersToClose = _openedUILayers.Keys.ToList();

            _uiStack.Clear();
            _idToIdentifier.Clear();
            _openedUILayers.Clear();

            foreach (var identifier in identifiersToClose)
            {
                _uiProvider.Close(identifier);
            }
        }

        internal void CloseLayer(UILayer layer, bool destroy = true)
        {
            EnsureProvider();

            var identifiersToClose = _openedUILayers
                .Where(kvp => kvp.Value == layer)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var identifier in identifiersToClose)
            {
                Close(identifier);
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
            if (_uiStack.Count == 0)
            {
                return false;
            }

            var lastNode = _uiStack.Last;
            if (lastNode == null)
            {
                return false;
            }

            Close(lastNode.Value);
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

        private void PublishCloseEvent(WindowIdentifier identifier, UILayer layer)
        {
            try
            {
                EventBus.Publish(new UICloseEvent
                {
                    Identifier = identifier,
                    Layer = layer,
                    IsDestroyed = true
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
            CloseAll();
            return base.OnShutdownAsync();
        }
    }
}
