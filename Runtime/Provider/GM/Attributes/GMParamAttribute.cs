#if JULYGF_DEBUG
using System;

namespace JulyCore.Provider.GM
{
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    public class GMParamAttribute : Attribute
    {
        public string DisplayName { get; }

        public GMParamAttribute(string displayName)
        {
            DisplayName = displayName;
        }
    }
}
#endif
