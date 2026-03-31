using JulyCore.Provider.Data;

namespace JulyCore.Module.Http
{
    public struct HttpResult<T>
    {
        public int Code;
        public string Msg;
        public int MsgId;
        public T Data;
        public bool IsOk => Code == 0;
    }

    public abstract class HttpEntityBase
    {
        public abstract string Path { get; }

        public int Code { get; internal set; }
        public string Msg { get; internal set; }
        public int RespMsgId { get; internal set; }
        public bool IsOk => Code == 0;

        internal abstract void SetResponseData(ISerializeProvider serializer, string dataJson);
        protected internal virtual void OnResponse() { }
        protected internal virtual void OnError() { }
    }

    public abstract class HttpEntity<TResp> : HttpEntityBase
    {
        public TResp RespData { get; internal set; }

        internal override void SetResponseData(ISerializeProvider serializer, string dataJson)
        {
            RespData = (TResp)serializer.DeserializeFromJson(dataJson, typeof(TResp));
        }
    }

    public abstract class HttpEntity<TReq, TResp> : HttpEntity<TResp>, IHttpRequestBody
    {
        public abstract TReq RqtData { get; }
        protected virtual object BuildBody() => RqtData;
        object IHttpRequestBody.GetBody() => BuildBody();
    }

    internal interface IHttpRequestBody
    {
        object GetBody();
    }
}
