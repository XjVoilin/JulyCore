using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Data.Network;

namespace JulyCore.Provider.Http
{
    /// <summary>
    /// HTTP 提供者接口
    /// </summary>
    public interface IHttpProvider : Core.IProvider
    {
        void ConfigureHttp(HttpConfig config);

        UniTask<HttpResponse> GetAsync(string url,
            Dictionary<string, string> headers = null,
            CancellationToken cancellationToken = default);

        UniTask<HttpResponse> PostAsync(string url, byte[] data,
            Dictionary<string, string> headers = null,
            CancellationToken cancellationToken = default);

        UniTask<HttpResponse> PostJsonAsync(string url, string jsonData,
            Dictionary<string, string> headers = null,
            CancellationToken cancellationToken = default);

        UniTask<HttpResponse> PutAsync(string url, byte[] data,
            Dictionary<string, string> headers = null,
            CancellationToken cancellationToken = default);

        UniTask<HttpResponse> DeleteAsync(string url,
            Dictionary<string, string> headers = null,
            CancellationToken cancellationToken = default);
    }
}
