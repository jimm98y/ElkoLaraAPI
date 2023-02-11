using System.Collections.Generic;

namespace ElkoLaraAPI.Http
{
    internal struct SimpleHttpResponse
    {
        public byte[] Content { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public int StatusCode { get; set; }
    }
}
