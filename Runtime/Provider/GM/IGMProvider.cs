#if JULYGF_DEBUG
using System;
using JulyCore.Core;
using TMPro;

namespace JulyCore.Provider.GM
{
    public interface IGMProvider : IProvider
    {
        void Register(Type type);
        void Build(TMP_FontAsset font = null);
    }
}
#endif
