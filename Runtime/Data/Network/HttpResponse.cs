using System;

namespace JulyCore.Data.Network
{
    [Serializable]
    public class HttpResponse
    {
        public int StatusCode { get; set; }
        public byte[] Data { get; set; }
        public string Error { get; set; }
        public long ElapsedMs { get; set; }
        public bool IsTimeout { get; set; }

        public bool IsSuccess => StatusCode >= 200 && StatusCode < 300 && string.IsNullOrEmpty(Error);

        public string GetText()
        {
            if (Data == null || Data.Length == 0)
                return string.Empty;
            return System.Text.Encoding.UTF8.GetString(Data);
        }
    }
}
