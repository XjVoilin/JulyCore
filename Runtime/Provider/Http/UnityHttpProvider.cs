using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Data.Network;
using JulyCore.Provider.Base;
using UnityEngine.Networking;

namespace JulyCore.Provider.Http
{
    public class UnityHttpProvider : ProviderBase, IHttpProvider
    {
        protected override LogChannel LogChannel => LogChannel.Network;
        public override int Priority => Frameworkconst.PriorityHttpProvider;

        public async UniTask<HttpResponse> SendAsync(string url, string method, byte[] body,
            Dictionary<string, string> headers, int timeoutSeconds,
            CancellationToken ct = default)
        {
            using var request = CreateRequest(url, method, body);

            if (headers != null)
            {
                foreach (var h in headers)
                    request.SetRequestHeader(h.Key, h.Value);
            }

            request.timeout = timeoutSeconds;

            try
            {
                await request.SendWebRequest().ToUniTask(cancellationToken: ct);
            }
            catch (UnityWebRequestException)
            {
                // ToUniTask() throws for non-2xx; swallow and fall through to unified result handling.
            }

            ct.ThrowIfCancellationRequested();

            var statusCode = (int)request.responseCode;
            var isNetworkError = request.result == UnityWebRequest.Result.ConnectionError
                              || request.result == UnityWebRequest.Result.DataProcessingError;
            var error = request.result != UnityWebRequest.Result.Success ? request.error : null;

            if (error != null)
                LogWarning($"[HTTP] <<< {statusCode} {url}: {error}");

            return new HttpResponse(statusCode, request.downloadHandler?.data, error, isNetworkError);
        }

        private static UnityWebRequest CreateRequest(string url, string method, byte[] body)
        {
            if (method == "GET")
                return UnityWebRequest.Get(url);

            var req = new UnityWebRequest(url, method)
            {
                downloadHandler = new DownloadHandlerBuffer()
            };

            if (body != null)
                req.uploadHandler = new UploadHandlerRaw(body) { contentType = "application/json" };

            return req;
        }

    }
}
