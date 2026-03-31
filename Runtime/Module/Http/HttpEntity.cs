using JulyCore.Provider.Data;

namespace JulyCore.Module.Http
{
    public struct HttpResult<T>
    {
        public int Code;
        public string Msg;
        public T Data;
        public bool IsOk => Code == 0;
    }

    public abstract class HttpEntityBase
    {
        public abstract string Path { get; }

        public int Code { get; internal set; }
        public string Msg { get; internal set; }
        public bool IsOk => Code == 0;

        internal abstract void SetResponseData(ISerializeProvider serializer, byte[] jsonBytes);
        protected internal virtual void OnResponse() { }
        protected internal virtual void OnError() { }
    }

    public abstract class HttpEntity<TResp> : HttpEntityBase
    {
        public TResp RespData { get; internal set; }

        internal override void SetResponseData(ISerializeProvider serializer, byte[] jsonBytes)
        {
            RespData = serializer.Deserialize<TResp>(jsonBytes);
        }
    }

    public abstract class HttpEntity<TReq, TResp> : HttpEntity<TResp>, IHttpRequestBody
    {
        public abstract TReq RqtData { get; }
        object IHttpRequestBody.GetBody() => RqtData;
    }

    internal interface IHttpRequestBody
    {
        object GetBody();
    }
}
