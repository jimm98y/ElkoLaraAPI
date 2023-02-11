using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ElkoLaraAPI
{
    /// <summary>
    /// Simple HTTP client that does not keep the connection opened and sends the entire request (header + content) in a single TCP packet.
    /// </summary>
    internal static class SimpleHttpClient
	{
        internal struct SimpleHttpResponse
        {
            public byte[] Content { get; set; }
            public Dictionary<string, string> Headers { get; set; }
            public int StatusCode { get; set; }
        }

        /// <summary>
        /// Sends the HTTP request and returns the response.
        /// </summary>
        /// <param name="method">HTTP method: GET or POST.</param>
        /// <param name="uri">Uri such as http://host.</param>
        /// <param name="msg">HTTP message body in bytes.</param>
        /// <returns><see cref="SimpleHttpResponse"/>.</returns>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public static async Task<SimpleHttpResponse> MakeRequestAsync(string method, string uri, Dictionary<string, string> headers = null, byte[] msg = null)
        {
            if (!(method == "POST" || method == "GET"))
                throw new NotSupportedException($"Method not supported: {method}");

            if (!Uri.IsWellFormedUriString(uri, UriKind.Absolute))
                throw new ArgumentException("Invalid URI.");

            SimpleHttpResponse result = new SimpleHttpResponse();
            Uri destination = new Uri(uri);

            using (var tcp = new TcpClient(AddressFamily.InterNetwork))
            {
                await tcp.ConnectAsync(destination.Host, destination.Port);

                using (var networkStream = tcp.GetStream())
                {
                    var builder = new StringBuilder();
                    builder.Append($"{method} {destination.PathAndQuery} HTTP/1.1\r\n");

                    if (headers == null || !headers.ContainsKey("Host"))
                        builder.Append($"Host: {destination.Host}\r\n");

                    if (method == "POST")
                    {
                        if(headers == null || !headers.ContainsKey("Content-Length"))
                            builder.Append($"Content-Length: {msg.Length}\r\n"); // only for POST request
                    }

                    if (headers == null || !headers.ContainsKey("Connection"))
                        builder.Append("Connection: close\r\n");

                    if (headers == null || !headers.ContainsKey("Accept"))
                        builder.Append("Accept: */*\r\n");

                    if (headers == null || !headers.ContainsKey("Accept-Encoding"))
                        builder.Append("Accept-Encoding: gzip, deflate\r\n");

                    if (headers == null || !headers.ContainsKey("Accept-Language"))
                        builder.Append("Accept-Language: en-US,en;q=0.9\r\n");

                    if (headers != null)
                    {
                        foreach (var item in headers)
                        {
                            builder.Append($"{item.Key}: {item.Value}\r\n");
                        }
                    }

                    builder.Append("\r\n");

                    string messageHeaders = builder.ToString();
                    byte[] message = Encoding.ASCII.GetBytes(messageHeaders);
                    
                    if (method == "POST" && msg != null)
                    {
                        message = message.Concat(msg).ToArray();
                    }

                    await networkStream.WriteAsync(message, 0, message.Length);
                    await networkStream.FlushAsync();

                    // receive data
                    using (var ms = new MemoryStream())
                    {
                        await LoadResponseAsync(networkStream, ms);

                        var data = ms.ToArray();
                        var index = BinaryMatch(data, Encoding.ASCII.GetBytes("\r\n\r\n")) + 4;
                        string[] responseHeaders = Encoding.ASCII.GetString(data, 0, index).Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                        
                        result.StatusCode = int.Parse(responseHeaders.First().Split(' ').Skip(1).First());
                        result.Headers = responseHeaders.Skip(1).ToDictionary(x => x.Substring(0, x.IndexOf(':')), x => (x.Substring(x.IndexOf(':') + 1)).TrimStart().TrimEnd());
                        result.Content = data.Skip(index).ToArray();

                        if (result.Headers != null && result.Headers.ContainsKey("Content-Length") && int.Parse(result.Headers["Content-Length"]) > result.Content.Length)
                        {
                            using (var contentMS = new MemoryStream())
                            {
                                await LoadResponseAsync(networkStream, contentMS);
                                contentMS.Position = 0;

                                if (result.Headers.ContainsKey("Content-Encoding") && result.Headers["Content-Encoding"] == "gzip")
                                {
                                    using (var decompressionStream = new GZipStream(contentMS, CompressionMode.Decompress))
                                    {
                                        using (var decompressedMemory = new MemoryStream())
                                        {
                                            decompressionStream.CopyTo(decompressedMemory);
                                            decompressedMemory.Position = 0;
                                            result.Content = decompressedMemory.ToArray();
                                        }
                                    }
                                }
                                else
                                {
                                    result.Content = contentMS.ToArray();
                                }
                            }
                        }
                    }
                }
            }

            return result;
        }

        private static async Task LoadResponseAsync(NetworkStream networkStream, MemoryStream ms)
        {
            var buffer = new byte[1024];
            while (true)
            {
                int byteCount = await networkStream.ReadAsync(buffer, 0, buffer.Length);
                ms.Write(buffer, 0, byteCount);

                if (byteCount < buffer.Length)
                {
                    break;
                }
            }
        }

        private static int BinaryMatch(byte[] input, byte[] pattern)
        {
            int len = input.Length - pattern.Length + 1;

            for (int i = 0; i < len; ++i)
            {
                bool match = true;

                for (int j = 0; j < pattern.Length; ++j)
                {
                    if (input[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}

