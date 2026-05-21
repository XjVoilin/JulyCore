#if JULYGF_URP
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace JulyCore.Provider.UI
{
    /// <summary>
    /// URP Camera Stack 工具：管理 UICamera 与场景 MainCamera 的 Overlay 合成关系。
    /// </summary>
    public static class CameraStackHandler
    {
        /// <summary>
        /// 将 UICamera 加入当前 MainCamera 的 Camera Stack（Overlay 模式）。
        /// 在场景加载完成后调用。
        /// </summary>
        public static void RebuildStack()
        {
            var uiCamera = GF.UI.UICamera;
            if (uiCamera == null) return;

            EnsureOverlay(uiCamera);

            var mainCamera = Camera.main;
            if (mainCamera == null) return;

            var mainData = mainCamera.GetUniversalAdditionalCameraData();
            if (mainData.cameraStack.Contains(uiCamera)) return;

            mainData.cameraStack.Add(uiCamera);
        }

        /// <summary>
        /// 场景切换前调用。
        /// 将 UICamera 临时提升为 Base 相机，避免旧场景 MainCamera 销毁后
        /// Overlay 失去 Base Camera 导致的黑屏闪烁。
        /// </summary>
        public static void PrepareForSceneSwitch()
        {
            var uiCamera = GF.UI.UICamera;
            if (uiCamera == null) return;

            var data = uiCamera.GetUniversalAdditionalCameraData();
            if (data.renderType != CameraRenderType.Overlay) return;

            var mainCamera = Camera.main;
            if (mainCamera != null)
                mainCamera.GetUniversalAdditionalCameraData().cameraStack.Remove(uiCamera);

            data.renderType = CameraRenderType.Base;
            uiCamera.clearFlags = CameraClearFlags.SolidColor;
            uiCamera.backgroundColor = Color.black;
        }

        private static void EnsureOverlay(Camera cam)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            if (data.renderType != CameraRenderType.Overlay)
                data.renderType = CameraRenderType.Overlay;
        }
    }
}
#endif
