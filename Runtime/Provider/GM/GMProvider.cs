#if JULYGF_DEBUG
using System;
using JulyCore.Core;
using JulyCore.Provider.Base;

namespace JulyCore.Provider.GM
{
    public class GMProvider : ProviderBase, IGMProvider
    {
        protected override LogChannel LogChannel => LogChannel.GM;

        private readonly GMRegistry _registry = new();
        private GMOverlayRoot _overlayRoot;

        public void Register(Type type)
        {
            _registry.Register(type);
        }

        public void Build(TMPro.TMP_FontAsset font = null)
        {
            if (font != null) GMUGUIPanel.OverrideFont = font;
            if (_overlayRoot != null) return;
            _overlayRoot = GMOverlayRoot.Create(_registry.Categories);
        }

        protected override void OnShutdown()
        {
            if (_overlayRoot != null)
            {
                UnityEngine.Object.Destroy(_overlayRoot.gameObject);
                _overlayRoot = null;
            }
            _registry.Clear();
        }
    }
}
#endif
