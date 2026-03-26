using JulyCore.Core;
using JulyCore.Provider.Platform;
using UnityEngine;
using UnityEngine.UI;

namespace JulyCore.Provider.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaAdapter : MonoBehaviour
    {
        private RectTransform _rect;
        private IPlatformProvider _platform;
        private CanvasScaler _canvasScaler;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            FrameworkContext.Instance?.Registry?.TryResolve(out _platform);
            _canvasScaler = GetComponentInParent<CanvasScaler>();
        }

        private void OnEnable()
        {
            ApplySafeArea();
            ApplyCanvasScalerMatch();
        }

        private void ApplySafeArea()
        {
            var safeArea = _platform?.GetSafeArea() ?? Screen.safeArea;

            float sw = Screen.width;
            float sh = Screen.height;
            if (sw <= 0 || sh <= 0) return;

            _rect.anchorMin = new Vector2(safeArea.x / sw, safeArea.y / sh);
            _rect.anchorMax = new Vector2(safeArea.xMax / sw, safeArea.yMax / sh);
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }

        private void ApplyCanvasScalerMatch()
        {
            if (_canvasScaler == null) return;

            var sw = Screen.width;
            var sh = Screen.height;
            if (sw <= 0 || sh <= 0) return;

            var refRes = _canvasScaler.referenceResolution;
            if (refRes.x <= 0 || refRes.y <= 0) return;

            var screenAspect = (float)sw / sh;
            var designAspect = refRes.x / refRes.y;
            _canvasScaler.matchWidthOrHeight = screenAspect > designAspect ? 1f : 0f;
        }
    }
}
