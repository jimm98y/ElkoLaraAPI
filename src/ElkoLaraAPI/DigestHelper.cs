using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ElkoLaraAPI
{
    /// <summary>
    /// Digest authentication helper.
    /// Implements the original RFC 2069 standard. RFC 2617 is not supported.
    /// </summary>
    /// <remarks>
    /// https://en.wikipedia.org/wiki/Digest_access_authentication
    /// https://stackoverflow.com/questions/594323/implement-digest-authentication-via-httpwebrequest-in-c-sharp
    /// </remarks>
    public class DigestHelper
    {
        public static string GetDigest(string wwwAuth, string requestUri, string httpMethod, string userName, string password)
        {
            var realm = GetHeaderVariable("realm", wwwAuth);
            var nonce = GetHeaderVariable("nonce", wwwAuth);

            var uri = new Uri(requestUri);
            var digest = BuildDigestHeader(userName, password, httpMethod, uri.PathAndQuery, realm, nonce);
            return digest;
        }

        private static string GetHeaderVariable(
            string varName,
            string header)
        {
            var regHeader = new Regex(string.Format(@"{0}=""([^""]*)""", varName));
            var matchHeader = regHeader.Match(header);
            if (matchHeader.Success)
                return matchHeader.Groups[1].Value;
            throw new ApplicationException(string.Format("Header {0} not found", varName));
        }

        private static string BuildDigestHeader(
            string userName,
            string password,
            string httpMethod,
            string uri,
            string realm,
            string nonce)
        {
            var ha1 = CalculateMd5Hash($"{userName}:{realm}:{password}");
            var ha2 = CalculateMd5Hash($"{httpMethod}:{uri}");
            var digestResponse = CalculateMd5Hash($"{ha1}:{nonce}:{ha2}");

            return "Digest "
                   + $"username=\"{userName}\", "
                   + $"realm=\"{realm}\", "
                   + $"nonce=\"{nonce}\", "
                   + $"uri=\"{uri}\", "
                   + $"response=\"{digestResponse}\"";
        }

        private static string CalculateMd5Hash(string input)
        {
            var bytes = Encoding.ASCII.GetBytes(input);
            using (var md5 = MD5.Create())
            {
                var hash = md5.ComputeHash(bytes);
                var builder = new StringBuilder();

                foreach (var b in hash)
                {
                    builder.Append(b.ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }
}
