using System;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Module.Base;
using JulyCore.Provider.Input;
using UnityEngine;

namespace JulyCore.Module.Input
{
    /// <summary>
    /// 输入管理模块
    /// 业务语义与流程调度层：管理输入屏蔽状态，组合过滤规则
    /// 不直接操作底层 Input API，通过 IInputProvider 获取原始输入
    /// </summary>
    internal class InputModule : ModuleBase
    {
        private IInputProvider _inputProvider;
        private int _blockCount;

        protected override LogChannel LogChannel => LogChannel.Input;
        public override int Priority => Frameworkconst.PriorityInputModule;

        protected override UniTask OnInitAsync()
        {
            _inputProvider = GetProvider<IInputProvider>();
            return base.OnInitAsync();
        }

        protected override void OnShutdown()
        {
            _blockCount = 0;
            _inputProvider = null;
        }

        #region 业务状态：屏蔽管理

        internal bool IsBlocked => _blockCount > 0;

        internal void Block() => _blockCount++;

        internal void Unblock() => _blockCount = Math.Max(0, _blockCount - 1);

        #endregion

        #region 业务规则：综合输入拦截判断

        /// <summary>
        /// 当前输入是否应被拦截。
        /// 综合 Block 计数 + EventSystem 检测。
        /// </summary>
        internal bool ShouldBlockInput(int fingerId = -1)
        {
            if (_blockCount > 0) return true;
            return _inputProvider.IsPointerOverGameObject(fingerId);
        }

        #endregion

        #region 业务规则：过滤后的单指输入

        /// <summary>
        /// 本帧是否有有效按下（已过滤 UI + 屏蔽）。
        /// </summary>
        internal bool GetPointerDown(out Vector2 screenPos)
        {
            screenPos = Vector2.zero;
            if (_blockCount > 0) return false;
            if (!_inputProvider.GetRawPointerDown(out screenPos)) return false;
            if (_inputProvider.IsPointerOverGameObject(GetCurrentFingerId()))
            {
                screenPos = Vector2.zero;
                return false;
            }
            return true;
        }

        /// <summary>
        /// 本帧 pointer 是否保持按住（受屏蔽影响）。
        /// </summary>
        internal bool GetPointerHeld(out Vector2 screenPos)
        {
            screenPos = Vector2.zero;
            if (_blockCount > 0) return false;
            return _inputProvider.GetRawPointerHeld(out screenPos);
        }

        /// <summary>
        /// 本帧 pointer 是否抬起（受屏蔽影响）。
        /// </summary>
        internal bool GetPointerUp(out Vector2 screenPos)
        {
            screenPos = Vector2.zero;
            if (_blockCount > 0) return false;
            return _inputProvider.GetRawPointerUp(out screenPos);
        }

        #endregion

        #region 业务规则：多指触控

        /// <summary>
        /// 当前触摸点数量（blocked 时返回 0）。
        /// </summary>
        internal int TouchCount => _blockCount > 0 ? 0 : _inputProvider.TouchCount;

        /// <summary>
        /// 获取指定索引的触摸数据（blocked 时返回 false）。
        /// </summary>
        internal bool TryGetTouch(int index, out Touch touch)
        {
            if (_blockCount > 0)
            {
                touch = default;
                return false;
            }
            return _inputProvider.TryGetTouch(index, out touch);
        }

        #endregion

        #region 位置查询

        /// <summary>
        /// 当前主指针屏幕坐标（纯查询，不受 Block 影响）。
        /// </summary>
        internal Vector2 PointerScreenPosition => _inputProvider.PointerScreenPosition;

        #endregion

        private int GetCurrentFingerId()
        {
            return _inputProvider.TouchCount > 0 && _inputProvider.TryGetTouch(0, out var touch)
                ? touch.fingerId
                : -1;
        }
    }
}
