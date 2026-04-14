using LitJson;

namespace JulyCore.Module.Http
{
    public abstract class HttpEntityBase
    {
        public const int CodeNetworkError = -1;
        public const int CodeParseError = -2;
        public const int CodeHttpError = -3;

        public abstract string Path { get; }
        public virtual string LogTag => null;

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
    }

    /// <summary>
    /// 直发请求的非泛型标记基类。Send 重载靠此类型隔离队列实体。
    /// </summary>
    public abstract class HttpEntity : HttpEntityBase { }

    public abstract class HttpEntity<TResp> : HttpEntity
    {
        public TResp RespData { get; protected set; }
    }

    public abstract class HttpEntity<TReq, TResp> : HttpEntity<TResp>
    {
        public abstract TReq RqtData { get; }
    }
}
