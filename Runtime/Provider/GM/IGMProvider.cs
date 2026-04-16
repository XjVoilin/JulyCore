#if JULYGF_DEBUG
using System;
using JulyCore.Core;
namespace JulyCore.Provider.GM
{
    public interface IGMProvider : IProvider
    {
        void Register(Type type);
        void Build();
    }
}
#endif
