using JulyCore.Module.Input;
using UnityEngine;

namespace JulyCore
{
    public static partial class GF
    {
        /// <summary>
        /// 输入相关操作
        /// </summary>
        public static class Input
        {
            private static InputModule _module;
            private static InputModule Module
            {
                get
                {
                    _module ??= GetModule<InputModule>();
                    return _module;
                }
            }

            /// <summary>
            /// 当前是否处于输入屏蔽状态
            /// </summary>
            public static bool IsBlocked => Module.IsBlocked;

            /// <summary>
            /// 增加屏蔽计数（支持嵌套，每次 Block 必须对应一次 Unblock）
            /// </summary>
            public static void Block() => Module.Block();

            /// <summary>
            /// 减少屏蔽计数
            /// </summary>
            public static void Unblock() => Module.Unblock();

            /// <summary>
            /// 当前输入是否应被拦截（综合 Block 计数 + EventSystem 检测）
            /// </summary>
            public static bool ShouldBlockInput(int fingerId = -1)
                => Module.ShouldBlockInput(fingerId);

            /// <summary>
            /// 本帧是否有有效按下（已过滤 UI 区域 + 屏蔽状态）
            /// </summary>
            public static bool GetPointerDown(out Vector2 screenPos)
                => Module.GetPointerDown(out screenPos);

            /// <summary>
            /// 本帧 pointer 是否保持按住（受屏蔽影响）
            /// </summary>
            public static bool GetPointerHeld(out Vector2 screenPos)
                => Module.GetPointerHeld(out screenPos);

            /// <summary>
            /// 本帧 pointer 是否抬起（受屏蔽影响）
            /// </summary>
            public static bool GetPointerUp(out Vector2 screenPos)
                => Module.GetPointerUp(out screenPos);

            /// <summary>
            /// 当前触摸点数量（屏蔽时返回 0）
            /// </summary>
            public static int TouchCount => Module.TouchCount;

            /// <summary>
            /// 获取指定索引的触摸数据（屏蔽时返回 false）
            /// </summary>
            public static bool TryGetTouch(int index, out Touch touch)
                => Module.TryGetTouch(index, out touch);

            /// <summary>
            /// 当前主指针屏幕坐标（纯查询，不受屏蔽影响）
            /// </summary>
            public static Vector2 PointerScreenPosition => Module.PointerScreenPosition;
        }
    }
}
