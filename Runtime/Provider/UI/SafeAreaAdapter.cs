using UnityEngine;

namespace JulyCore.Provider.UI
{
    /// <summary>
    /// Safe Area 适配组件
    /// 自动调整 RectTransform 到设备安全区域，处理刘海屏、圆角、底部手势条等遮挡
    /// 由 UIProvider 在创建 Layer 时自动添加，无需手动挂载
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaAdapter : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _lastSafeArea;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            ApplySafeArea();
        }

        private void Update()
        {
            if (_lastSafeArea != Screen.safeArea)
            {
                ApplySafeArea();
            }
        }

        private void ApplySafeArea()
        {
            var safeArea = Screen.safeArea;
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
