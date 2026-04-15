using System;
using System.Collections.Generic;
using JulyCore.Data.Save;

namespace JulyCore.Module.Http
{
    [Serializable]
    public class HttpPendingQueueData : ISaveData
    {
        public SaveImportance Importance => SaveImportance.Critical;
        public List<HttpPendingEntry> Entries = new();
    }

    [Serializable]
    public class HttpPendingEntry
    {
        public string Path;
        public string Body;
    }
}
