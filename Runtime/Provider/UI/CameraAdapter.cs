using JulyCore.Core;
using UnityEngine;

namespace JulyCore.Provider.UI
{
    [RequireComponent(typeof(Camera))]
    public class CameraAdapter : MonoBehaviour
    {
        private Camera _cam;
        private float _designOrthoSize;
        private Vector2 _designResolution;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _designOrthoSize = _cam.orthographicSize;

            var uiConfig = FrameworkContext.Instance?.FrameworkConfig?.UIConfig;
            _designResolution = uiConfig?.DesignResolution ?? new Vector2(1080, 1920);
        }

        private void OnEnable() => Apply();

        private void Apply()
        {
            if (_cam == null || !_cam.orthographic) return;
            if (_designResolution.x <= 0 || _designResolution.y <= 0) return;

            var sw = Screen.width;
            var sh = Screen.height;
            if (sw <= 0 || sh <= 0) return;

            var screenAspect = (float)sw / sh;
            var designAspect = _designResolution.x / _designResolution.y;

            _cam.orthographicSize = screenAspect >= designAspect
                ? _designOrthoSize
                : _designOrthoSize * designAspect / screenAspect;
        }
    }
}
