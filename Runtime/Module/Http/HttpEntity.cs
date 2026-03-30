namespace JulyCore.Module.Http
{
    public struct HttpResult<T>
    {
        public int Code;
        public string Msg;
        public T Data;
        public bool IsOk => Code == 0;
    }

    public abstract class HttpEntity<TResp>
    {
        public abstract string Path { get; }

        public int Code { get; internal set; }
        public string Msg { get; internal set; }
        public TResp RespData { get; internal set; }
        public bool IsOk => Code == 0;

        protected internal virtual void OnResponse() { }
        protected internal virtual void OnError() { }
    }

    public abstract class HttpEntity<TReq, TResp> : HttpEntity<TResp>, IHttpRequestBody
    {
        public TReq RqtData { get; set; }
        object IHttpRequestBody.GetBody() => RqtData;
    }

    internal interface IHttpRequestBody
    {
        object GetBody();
    }
}
