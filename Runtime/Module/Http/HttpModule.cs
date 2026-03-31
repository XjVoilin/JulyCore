using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Data.Network;
using JulyCore.Module.Base;
using JulyCore.Provider.Data;
using JulyCore.Provider.Http;
using LitJson;

namespace JulyCore.Module.Http
{
    public class HttpModule : ModuleBase
    {
        private IHttpProvider _provider;
        private ISerializeProvider _serializer;

        private string _baseUrl;
        private int _timeoutSeconds = 15;
        private Dictionary<string, string> _defaultHeaders;

        protected override LogChannel LogChannel => LogChannel.Network;
        public override int Priority => Frameworkconst.PriorityHttpModule;

        protected override UniTask OnInitAsync()
        {
            _provider = GetProvider<IHttpProvider>();
            if (_provider == null)
                LogWarning($"[{Name}] IHttpProvider 未注册，HTTP 功能不可用");

            _serializer = GetProvider<ISerializeProvider>();
            if (_serializer == null)
                LogWarning($"[{Name}] ISerializeProvider 未注册，序列化不可用");

            return UniTask.CompletedTask;
        }

        protected override UniTask OnShutdownAsync()
        {
            _provider = null;
            _serializer = null;
            _defaultHeaders = null;
            return UniTask.CompletedTask;
        }

        public void Configure(string baseUrl, int timeoutSeconds = 15)
        {
            _baseUrl = baseUrl;
            _timeoutSeconds = timeoutSeconds;
        }

        public void SetDefaultHeader(string key, string value)
        {
            _defaultHeaders ??= new Dictionary<string, string>();
            _defaultHeaders[key] = value;
        }

        public void RemoveDefaultHeader(string key)
        {
            _defaultHeaders?.Remove(key);
        }

        public async UniTask Send(HttpEntityBase entity, CancellationToken ct = default)
        {
            object body = entity is IHttpRequestBody rb ? rb.GetBody() : null;
            var raw = await SendRawAsync(entity.Path, body, ct);

            entity.Code = raw.Code;
            entity.Msg = raw.Msg;
            entity.RespMsgId = raw.MsgId;

            if (raw.IsOk && raw.DataJson != null)
                entity.SetResponseData(_serializer, raw.DataJson);

            if (entity.IsOk)
                entity.OnResponse();
            else
                entity.OnError();
        }

        public async UniTask<T> Send<T>(CancellationToken ct = default)
            where T : HttpEntityBase, new()
        {
            var entity = new T();
            await Send(entity, ct);
            return entity;
        }

        public async UniTask<HttpResult<TResp>> SendRequest<TResp>(
            string path, object body = null, CancellationToken ct = default)
        {
            var raw = await SendRawAsync(path, body, ct);
            var result = new HttpResult<TResp>
            {
                Code = raw.Code,
                Msg = raw.Msg,
                MsgId = raw.MsgId
            };

            if (result.IsOk && raw.DataJson != null)
                result.Data = (TResp)_serializer.DeserializeFromJson(raw.DataJson, typeof(TResp));

            return result;
        }

        #region Internal

        private struct RawResult
        {
            public int Code;
            public string Msg;
            public int MsgId;
            public string DataJson;
            public bool IsOk => Code == 0;
        }

        private async UniTask<RawResult> SendRawAsync(string path, object body, CancellationToken ct)
        {
            if (_provider == null)
                throw new InvalidOperationException("IHttpProvider 未注册");
            if (_serializer == null)
                throw new InvalidOperationException("ISerializeProvider 未注册");

            var url = BuildUrl(path);
            byte[] bodyBytes = null;
            var method = "GET";

            if (body != null)
            {
                var json = body is string s ? s : _serializer.SerializeToJson(body);
                bodyBytes = Encoding.UTF8.GetBytes(json);
                method = "POST";
            }

            HttpResponse raw;
            try
            {
                raw = await _provider.SendAsync(url, method, bodyBytes, _defaultHeaders, _timeoutSeconds, ct);
            }
            catch (OperationCanceledException)
            {
                return new RawResult { Code = -1, Msg = "请求已取消" };
            }
            catch (Exception ex)
            {
                LogError($"[HTTP] 请求异常 {path}: {ex.Message}");
                return new RawResult { Code = -1, Msg = ex.Message };
            }

            if (!raw.IsSuccess)
            {
                LogWarning($"[HTTP] 请求失败 {path}: {raw.StatusCode} {raw.Error}");
                return new RawResult { Code = -1, Msg = raw.Error ?? $"HTTP {raw.StatusCode}" };
            }

            return ParseEnvelope(path, raw.GetText());
        }

        /// <summary>
        /// 解析 Gate 响应信封。
        /// 成功: { "code": 0, "msg_id": 101, "data": {...} }
        /// 错误: { "code": 105, "msg": "login failed" }
        /// </summary>
        private RawResult ParseEnvelope(string path, string text)
        {
            try
            {
                var jd = JsonMapper.ToObject(text);
                var code = jd.ContainsKey("code") ? (int)jd["code"] : 0;

                if (code != 0)
                {
                    var msg = jd.ContainsKey("msg") ? (string)jd["msg"] : null;
                    LogWarning($"[HTTP] 业务错误 {path}: code={code} msg={msg}");
                    return new RawResult { Code = code, Msg = msg };
                }

                var result = new RawResult
                {
                    Code = 0,
                    MsgId = jd.ContainsKey("msg_id") ? (int)jd["msg_id"] : 0
                };

                if (jd.ContainsKey("data") && jd["data"] != null)
                    result.DataJson = jd["data"].ToJson();

                return result;
            }
            catch (Exception ex)
            {
                LogError($"[HTTP] 响应解析失败 {path}: {ex.Message}");
                return new RawResult { Code = -1, Msg = $"响应解析失败: {ex.Message}" };
            }
        }

        private string BuildUrl(string path)
        {
            if (string.IsNullOrEmpty(_baseUrl) || path.StartsWith("http"))
                return path;
            return _baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
        }

        #endregion
    }
}
