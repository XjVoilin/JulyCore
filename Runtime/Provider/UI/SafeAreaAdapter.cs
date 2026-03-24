using JulyCore.Core;
using JulyCore.Provider.Platform;
using UnityEngine;

namespace JulyCore.Provider.UI
{
    /// <summary>
    /// Safe Area 适配组件
    /// 自动调整 RectTransform 到设备安全区域，处理刘海屏、圆角、底部手势条等遮挡
    /// 由 UIProvider 在创建 Layer 时自动添加，无需手动挂载
    /// 优先使用 IPlatformProvider.GetSafeArea()，未注册时回退到 Screen.safeArea
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaAdapter : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _lastSafeArea;
        private IPlatformProvider _platform;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            FrameworkContext.Instance?.Registry?.TryResolve(out _platform);
        }

        private void OnEnable()
        {
            ApplySafeArea();
        }

        private void Update()
        {
            var current = _platform?.GetSafeArea() ?? Screen.safeArea;
            if (_lastSafeArea != current)
            {
                ApplySafeArea();
            }
        }

        private void ApplySafeArea()
        {
            var safeArea = _platform?.GetSafeArea() ?? Screen.safeArea;
            _lastSafeArea = safeArea;

            var screenW = (float)Screen.width;
            var screenH = (float)Screen.height;

            if (screenW <= 0 || screenH <= 0) return;

            _rect.anchorMin = new Vector2(safeArea.x / screenW, safeArea.y / screenH);
            _rect.anchorMax = new Vector2(safeArea.xMax / screenW, safeArea.yMax / screenH);
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
