#if JULYGF_DEBUG
using JulyCore.Provider.GM;

namespace JulyCore
{
    public static partial class GF
    {
        public static IGMProvider GM => Resolve<IGMProvider>();
    }
}
#endif
