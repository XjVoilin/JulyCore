using System;
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
            return UniTask.CompletedTask;
        }

        public void Configure(string baseUrl, int timeoutSeconds = 15)
        {
            _baseUrl = baseUrl;
            _timeoutSeconds = timeoutSeconds;
        }

        /// <summary>
        /// Entity 模式：发送请求并填充 entity 结果
        /// </summary>
        public async UniTask Send(HttpEntityBase entity, CancellationToken ct = default)
        {
            object body = entity is IHttpRequestBody rb ? rb.GetBody() : null;
            var raw = await SendRawAsync(entity.Path, body, ct);

            entity.Code = raw.Code;
            entity.Msg = raw.Msg;

            if (raw.IsOk && raw.DataBytes != null)
                entity.SetResponseData(_serializer, raw.DataBytes);

            if (entity.IsOk)
                entity.OnResponse();
            else
                entity.OnError();
        }

        /// <summary>
        /// 底层管线：序列化 → 发送 → 信封解析 → 反序列化，返回结构化结果
        /// </summary>
        public async UniTask<HttpResult<TResp>> SendRequest<TResp>(
            string path, object body = null, CancellationToken ct = default)
        {
            var raw = await SendRawAsync(path, body, ct);
            var result = new HttpResult<TResp> { Code = raw.Code, Msg = raw.Msg };

            if (result.IsOk && raw.DataBytes != null)
                result.Data = _serializer.Deserialize<TResp>(raw.DataBytes);

            return result;
        }

        private struct RawResult
        {
            public int Code;
            public string Msg;
            public byte[] DataBytes;
            public bool IsOk => Code == 0;
        }

        private async UniTask<RawResult> SendRawAsync(
            string path, object body, CancellationToken ct)
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
                bodyBytes = Encoding.UTF8.GetBytes(_serializer.SerializeToJson(body));
                method = "POST";
            }

            HttpResponse raw;
            try
            {
                raw = await _provider.SendAsync(url, method, bodyBytes, null, _timeoutSeconds, ct);
            }
            catch (OperationCanceledException)
            {
                return new RawResult { Code = -1, Msg = "请求已取消" };
            }
            catch (Exception ex)
            {
                return new RawResult { Code = -1, Msg = ex.Message };
            }

            if (!raw.IsSuccess)
                return new RawResult { Code = -1, Msg = raw.Error ?? $"HTTP {raw.StatusCode}" };

            try
            {
                var text = raw.GetText();
                var jd = JsonMapper.ToObject(text);

                var result = new RawResult
                {
                    Code = jd.ContainsKey("code") ? (int)jd["code"] : 0,
                    Msg = jd.ContainsKey("msg") ? (string)jd["msg"] : null
                };

                if (result.IsOk && jd.ContainsKey("data") && jd["data"] != null)
                    result.DataBytes = Encoding.UTF8.GetBytes(jd["data"].ToJson());

                return result;
            }
            catch (Exception ex)
            {
                return new RawResult { Code = -1, Msg = $"响应解析失败: {ex.Message}" };
            }
        }

        private string BuildUrl(string path)
        {
            if (string.IsNullOrEmpty(_baseUrl) || path.StartsWith("http"))
                return path;
            return _baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
        }
    }
}
