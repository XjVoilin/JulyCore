using JulyCore.Core;
using JulyCore.Provider.Base;
using UnityEngine;
using UnityEngine.EventSystems;

namespace JulyCore.Provider.Input
{
    /// <summary>
    /// Unity 输入提供者实现
    /// 纯技术执行层：封装 UnityEngine.Input 跨平台读取 + EventSystem UI 检测
    /// 不包含任何业务语义，不维护业务状态
    /// </summary>
    internal class UnityInputProvider : ProviderBase, IInputProvider
    {
        public override int Priority => Frameworkconst.PriorityInputProvider;
        protected override LogChannel LogChannel => LogChannel.Input;

        public bool GetRawPointerDown(out Vector2 screenPos)
        {
            screenPos = Vector2.zero;
#if UNITY_EDITOR || UNITY_STANDALONE
            if (!UnityEngine.Input.GetMouseButtonDown(0)) return false;
            screenPos = UnityEngine.Input.mousePosition;
            return true;
#else
            if (UnityEngine.Input.touchCount <= 0) return false;
            var touch = UnityEngine.Input.GetTouch(0);
            if (touch.phase != TouchPhase.Began) return false;
            screenPos = touch.position;
            return true;
#endif
        }

        public bool GetRawPointerHeld(out Vector2 screenPos)
        {
            screenPos = Vector2.zero;
#if UNITY_EDITOR || UNITY_STANDALONE
            if (!UnityEngine.Input.GetMouseButton(0)) return false;
            screenPos = UnityEngine.Input.mousePosition;
            return true;
#else
            if (UnityEngine.Input.touchCount <= 0) return false;
            var touch = UnityEngine.Input.GetTouch(0);
            if (touch.phase != TouchPhase.Moved && touch.phase != TouchPhase.Stationary) return false;
            screenPos = touch.position;
            return true;
#endif
        }

        public bool GetRawPointerUp(out Vector2 screenPos)
        {
            screenPos = Vector2.zero;
#if UNITY_EDITOR || UNITY_STANDALONE
            if (!UnityEngine.Input.GetMouseButtonUp(0)) return false;
            screenPos = UnityEngine.Input.mousePosition;
            return true;
#else
            if (UnityEngine.Input.touchCount <= 0) return false;
            var touch = UnityEngine.Input.GetTouch(0);
            if (touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled) return false;
            screenPos = touch.position;
            return true;
#endif
        }

        public int TouchCount => UnityEngine.Input.touchCount;

        public bool TryGetTouch(int index, out Touch touch)
        {
            if (index >= 0 && index < UnityEngine.Input.touchCount)
            {
                touch = UnityEngine.Input.GetTouch(index);
                return true;
            }
            touch = default;
            return false;
        }

        public Vector2 PointerScreenPosition
        {
            get
            {
#if UNITY_EDITOR || UNITY_STANDALONE
                return UnityEngine.Input.mousePosition;
#else
                return UnityEngine.Input.touchCount > 0
                    ? UnityEngine.Input.GetTouch(0).position
                    : Vector2.zero;
#endif
            }
        }

        public bool IsPointerOverGameObject(int fingerId = -1)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            return fingerId >= 0
                ? es.IsPointerOverGameObject(fingerId)
                : es.IsPointerOverGameObject();
        }
    }
}
