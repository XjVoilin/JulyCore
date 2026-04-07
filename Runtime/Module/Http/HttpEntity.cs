using LitJson;

namespace JulyCore.Module.Http
{
    public abstract class HttpEntityBase
    {
        public abstract string Path { get; }

        public int Code { get; protected internal set; }
        public string Msg { get; protected internal set; }
        public int RespMsgId { get; protected internal set; }
        public bool IsOk => Code == 0;

        protected internal virtual string BuildBody() => null;

        protected internal virtual void ParseResponse(string responseText)
        {
            var jd = JsonMapper.ToObject(responseText);
            Code = jd.ContainsKey("code") ? (int)jd["code"] : 0;
            Msg = jd.ContainsKey("msg") ? (string)jd["msg"] : null;
            RespMsgId = jd.ContainsKey("msg_id") ? (int)jd["msg_id"] : 0;

            if (Code == 0 && jd.ContainsKey("data") && jd["data"] != null)
                SetResponseData(jd["data"].ToJson());
        }

        protected abstract void SetResponseData(string dataJson);

        protected internal virtual void OnResponse() { }
        protected internal virtual void OnError() { }
    }

    public abstract class HttpEntity<TResp> : HttpEntityBase
    {
        public TResp RespData { get; protected set; }
    }

    public abstract class HttpEntity<TReq, TResp> : HttpEntity<TResp>
    {
        public abstract TReq RqtData { get; }
    }
}
