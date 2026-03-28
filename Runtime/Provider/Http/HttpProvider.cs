using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Data.Network;
using JulyCore.Provider.Base;
using UnityEngine;
using UnityEngine.Networking;

namespace JulyCore.Provider.Http
{
    internal class HttpProvider : ProviderBase, IHttpProvider
    {
        protected override LogChannel LogChannel => LogChannel.Network;

        public override int Priority => Frameworkconst.PriorityHttpProvider;

        private HttpConfig _config = new HttpConfig();

        public void ConfigureHttp(HttpConfig config)
        {
            _config = config ?? new HttpConfig();
        }

        public async UniTask<HttpResponse> GetAsync(string url,
            Dictionary<string, string> headers = null,
            CancellationToken cancellationToken = default)
        {
            return await SendAsync("GET", url, null, headers, cancellationToken);
        }

        public async UniTask<HttpResponse> PostAsync(string url, byte[] data,
            Dictionary<string, string> headers = null,
            CancellationToken cancellationToken = default)
        {
            return await SendAsync("POST", url, data, headers, cancellationToken);
        }

        public async UniTask<HttpResponse> PostJsonAsync(string url, string jsonData,
            Dictionary<string, string> headers = null,
            CancellationToken cancellationToken = default)
        {
            headers ??= new Dictionary<string, string>();
            if (!headers.ContainsKey("Content-Type"))
                headers["Content-Type"] = "application/json";

            var data = Encoding.UTF8.GetBytes(jsonData ?? string.Empty);
            return await SendAsync("POST", url, data, headers, cancellationToken);
        }

        public async UniTask<HttpResponse> PutAsync(string url, byte[] data,
            Dictionary<string, string> headers = null,
            CancellationToken cancellationToken = default)
        {
            return await SendAsync("PUT", url, data, headers, cancellationToken);
        }

        public async UniTask<HttpResponse> DeleteAsync(string url,
            Dictionary<string, string> headers = null,
            CancellationToken cancellationToken = default)
        {
            return await SendAsync("DELETE", url, null, headers, cancellationToken);
        }

        private async UniTask<HttpResponse> SendAsync(string method, string url,
            byte[] data, Dictionary<string, string> headers,
            CancellationToken cancellationToken)
        {
            var startTime = UnityEngine.Time.realtimeSinceStartup;

            if (!string.IsNullOrEmpty(_config.BaseUrl) && !url.StartsWith("http"))
                url = _config.BaseUrl.TrimEnd('/') + "/" + url.TrimStart('/');

            var retryCount = 0;
            var maxRetries = _config.EnableRetry ? _config.MaxRetryCount : 0;

            while (true)
            {
                try
                {
                    using var request = CreateRequest(method, url, data);

                    if (_config.DefaultHeaders != null)
                    {
                        foreach (var h in _config.DefaultHeaders)
                            request.SetRequestHeader(h.Key, h.Value);
                    }

                    if (headers != null)
                    {
                        foreach (var h in headers)
                            request.SetRequestHeader(h.Key, h.Value);
                    }

                    request.timeout = (int)_config.TimeoutSeconds;

                    await request.SendWebRequest().ToUniTask(
                        cancellationToken: cancellationToken);

                    var response = BuildResponse(request, startTime);

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        response.Error = request.error
                            ?? $"HTTP Error: {request.responseCode}";

                        if (retryCount < maxRetries && ShouldRetry((int)request.responseCode))
                        {
                            retryCount++;
                            Log($"HTTP 请求失败，重试 ({retryCount}/{maxRetries}): {url}");
                            await UniTask.Delay(
                                TimeSpan.FromSeconds(_config.RetryIntervalSeconds),
                                cancellationToken: cancellationToken);
                            continue;
                        }
                    }

                    return response;
                }
                catch (OperationCanceledException)
                {
                    return new HttpResponse
                    {
                        StatusCode = 0,
                        Error = "请求已取消",
                        ElapsedMs = ElapsedMs(startTime)
                    };
                }
                catch (Exception ex)
                {
                    if (retryCount < maxRetries)
                    {
                        retryCount++;
                        LogWarning($"HTTP 请求异常，重试 ({retryCount}/{maxRetries}): {ex}");
                        await UniTask.Delay(
                            TimeSpan.FromSeconds(_config.RetryIntervalSeconds),
                            cancellationToken: cancellationToken);
                        continue;
                    }

                    Debug.LogException(ex);
                    return new HttpResponse
                    {
                        StatusCode = 0,
                        Error = ex.Message,
                        ElapsedMs = ElapsedMs(startTime)
                    };
                }
            }
        }

        private static UnityWebRequest CreateRequest(string method, string url, byte[] data)
        {
            switch (method)
            {
                case "GET":
                    return UnityWebRequest.Get(url);
                case "DELETE":
                    return UnityWebRequest.Delete(url);
                default:
                    var req = new UnityWebRequest(url, method)
                    {
                        uploadHandler = new UploadHandlerRaw(data),
                        downloadHandler = new DownloadHandlerBuffer()
                    };
                    return req;
            }
        }

        private static HttpResponse BuildResponse(UnityWebRequest request, float startTime)
        {
            var response = new HttpResponse
            {
                StatusCode = (int)request.responseCode,
                Data = request.downloadHandler?.data,
                Headers = new Dictionary<string, string>(),
                ElapsedMs = ElapsedMs(startTime)
            };

            var respHeaders = request.GetResponseHeaders();
            if (respHeaders != null)
            {
                foreach (var h in respHeaders)
                    response.Headers[h.Key] = h.Value;
            }

            return response;
        }

        private bool ShouldRetry(int statusCode)
        {
            return _config.RetryStatusCodes?.Contains(statusCode) ?? false;
        }

        private static long ElapsedMs(float startTime)
        {
            return (long)((UnityEngine.Time.realtimeSinceStartup - startTime) * 1000);
        }
    }
}
