#if JULYGF_DEBUG
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JulyCore.Provider.GM
{
    public sealed class GMOverlayRoot : MonoBehaviour
    {
        public static GMOverlayRoot Create(IReadOnlyList<GMCategoryInfo> categories)
        {
            var go = new GameObject("[GM Overlay]");
            DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            var root = go.AddComponent<GMOverlayRoot>();

            var panel = go.AddComponent<GMIMGUIPanel>();
            panel.Init(categories);

            // ball first, then blocker on top — blocker blocks ball clicks when panel is open
            GMFloatingBall.Create(go.transform, () => panel.Show());
            var blocker = CreateBlocker(go.transform);
            panel.Blocker = blocker;

            return root;
        }

        private static GameObject CreateBlocker(Transform parent)
        {
            var go = new GameObject("Blocker", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = Color.clear;
            img.raycastTarget = true;

            go.SetActive(false);
            return go;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            var es = new GameObject("[EventSystem]");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(es);
        }
    }
}
#endif
