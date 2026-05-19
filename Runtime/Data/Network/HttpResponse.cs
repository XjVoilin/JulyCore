namespace JulyCore.Data.Network
{
    public readonly struct HttpResponse
    {
        public readonly int StatusCode;
        public readonly byte[] Data;
        public readonly string Error;
        public readonly bool IsNetworkError;

        public HttpResponse(int statusCode, byte[] data, string error, bool isNetworkError)
        {
            StatusCode = statusCode;
            Data = data;
            Error = error;
            IsNetworkError = isNetworkError;
        }

        /// <summary>HTTP 状态码 2xx</summary>
        public bool IsHttpOk => StatusCode >= 200 && StatusCode < 300;

        /// <summary>有 response body（无论状态码）</summary>
        public bool HasBody => Data != null && Data.Length > 0;

        /// <summary>请求到达了服务器并拿到了响应</summary>
        public bool HasResponse => StatusCode > 0;

        public string GetText()
        {
            if (Data == null || Data.Length == 0)
                return string.Empty;
            return System.Text.Encoding.UTF8.GetString(Data);
        }
    }
}
