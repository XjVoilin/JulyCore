#if JULYGF_DEBUG
using System.Collections.Generic;

namespace JulyCore.Provider.GM
{
    public sealed class GMCategoryInfo
    {
        public string Category;
        public List<GMCommandInfo> Commands = new();
    }
}
#endif
