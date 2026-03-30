using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Data.Network;

namespace JulyCore.Provider.Http
{
    public interface IHttpProvider : Core.IProvider
    {
        UniTask<HttpResponse> SendAsync(string url, string method, byte[] body,
            Dictionary<string, string> headers, int timeoutSeconds,
            CancellationToken ct = default);
    }
}
