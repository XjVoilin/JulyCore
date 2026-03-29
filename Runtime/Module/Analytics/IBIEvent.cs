using System.Collections.Generic;

namespace JulyCore
{
    public interface IBIEvent
    {
        string EventName { get; }
        Dictionary<string, object> ToParams();
    }
}
