using ElkoLaraAPI.API;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ElkoLaraAPI
{
    public class ElkoLaraClient : IDisposable
    {
        private const int MAX_SETTINGS_STRING = 16;
        private static readonly Encoding _encoding = null;

        private readonly string _host;
        private readonly string _userName;
        private readonly string _password;

        private readonly HttpClient _httpClient;
        private readonly Random _rand = new Random();
        private bool disposedValue;
        private string _wwwAuth;

        static ElkoLaraClient()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _encoding = Encoding.GetEncoding(1250);
        }

        public ElkoLaraClient(string ipAddress, string userName, string password)
        {
            this._host = $"http://{ipAddress ?? throw new ArgumentNullException(nameof(ipAddress))}";
            this._userName = userName ?? throw new ArgumentNullException(nameof(userName));
            this._password = password ?? throw new ArgumentNullException(nameof(password));
            this._httpClient = new HttpClient();
        }

        public async Task<string> GetIndexPageAsync()
        {
            byte[] response = await MakeRequestAsync("GET", this._host);
            return Encoding.UTF8.GetString(response);
        }

        public async Task<LaraBasicInfo> GetBasicInfoAsync()
        {
            byte[] request = new byte[]
            {
                255,
                250,
                250,
                255,
                (byte)Math.Floor(125 * _rand.NextDouble() + 1),
                (byte)Math.Floor(125 * _rand.NextDouble() + 125 + 1),
                0,
                128,
                2
            };

            byte[] response = await MakeRequestAsync("POST", $"{this._host}/data", request);

            int t = response[11] << 16 | response[12] << 8 | response[13];
            string infoFw = ((int)Math.Floor(t / 1e4) + "." + Math.Floor((t - 10000 * Math.Floor(t / 10000d)) / 1000)) + "." + (t % 1000 < 100 ? "0" + t % 1000 : "" + t % 1000);
            string infoHw = 1 == response[14] ? "version A" : "version B";
            string infoIp = response[15] + "." + response[16] + "." + response[17] + "." + response[18];
            string infoName = _encoding.GetString(response.Skip(19).Take(MAX_SETTINGS_STRING).ToArray()).TrimEnd('\0');

            return new LaraBasicInfo()
            {
                Firmware = infoFw,
                Hardware = infoHw,
                IpAddress = infoIp,
                Name = infoName
            };
        }

        public Task<LaraOpenResult> PlayAsync()
        {
            return OpenRemoteAsync(3, 0);
        }

        public Task<LaraOpenResult> StopAsync()
        {
            return OpenRemoteAsync(4, 0);
        }

        public Task<LaraOpenResult> SetVolumeAsync(int volume)
        {
            if (volume < 0 || volume > 100)
                throw new ArgumentOutOfRangeException(nameof(volume));

            return OpenRemoteAsync(5, (byte)volume);
        }

        public async Task<LaraSettings> GetSettingsAsync()
        {
            byte[] request = new byte[]
            {
                255,
                250,
                250,
                255,
                (byte)Math.Floor(125 * _rand.NextDouble() + 1),
                (byte)Math.Floor(125 * _rand.NextDouble() + 125 + 1),
                0,
                128,
                192,
                2
            };

            byte[] response = await MakeRequestAsync("POST", $"{this._host}/data", request);

            byte[] controlBits = new byte[2] { response[11], response[12] };
            string ipAddress = $"{response[81]}.{response[82]}.{response[83]}.{response[84]}";
            string ipMask = $"{response[85]}.{response[86]}.{response[87]}.{response[88]}";
            string ipGateway = $"{response[89]}.{response[90]}.{response[91]}.{response[92]}";
            byte watchdogPeriod = response[93];
            byte watchdogCount = response[94];
            string ipAz = $"{response[97]}.{response[98]}.{response[99]}.{response[100]}";
            string ipSntp = $"{response[118]}.{response[119]}.{response[120]}.{response[121]}";
            string ipDns = $"{response[123]}.{response[124]}.{response[125]}.{response[126]}";
            byte language = response[127];
            byte ringtone = response[128];
            byte ringVolume = response[129];
            byte callVolume = response[130];
            byte micGain = response[131];
            byte utc = (128 & response[122]) > 0 ? (byte)(response[122] - 255 - 1) : response[122];

            string deviceName = ParseString(response, 101);
            string adminLogin = ParseString(response, 13);
            string adminPassword = ParseString(response, 30);
            string userLogin = ParseString(response, 47);
            string userPassword = ParseString(response, 64);
            string audioZoneUserName = ParseString(response, 132);
            string audioZonePassword = ParseString(response, 149);

            return new LaraSettings()
            {
                ControlBits = controlBits,
                IpAddress = ipAddress,
                IpMask = ipMask,
                IpGateway = ipGateway,
                WatchdogPeriod = watchdogPeriod,
                WatchdogCount = watchdogCount,
                IpAz = ipAz,
                IpSntp = ipSntp,
                IpDns = ipDns,
                Language = language,
                Ringtone = ringtone,
                RingVolume = ringVolume,
                CallVolume = callVolume,
                MicGain = micGain,
                Utc = utc,
                DeviceName = deviceName,
                AdminLogin = adminLogin,
                AdminPassword = adminPassword,
                UserLogin = userLogin,
                UserPassword = userPassword,
                AudioZoneUserName = audioZoneUserName,
                AudioZonePassword = audioZonePassword
            };
        }

        public Task SetSettingsAsync(LaraSettings settings)
        {
            throw new NotImplementedException();
        }

        // TODO: move state
        private const byte MAX_NAME_STRING = 12;
        private const byte MAX_DOMAIN_NAME_STRING = 49;
        private const byte MAX_FILE_NAME_STRING = 69;
        private const byte MAX_STATIONS = 40;
        private byte stations_count = 0;
        private byte audio_mute = 0;
        private bool is_playing = false;
        private string[] station_name = new string[MAX_STATIONS];

        private async Task<LaraOpenResult> OpenRemoteAsync(byte e, byte t)
        {
            byte[] request = new byte[]
            {
                255,
                251,
                251,
                204,
                e,
                t
            };

            byte[] response = await MakeRequestAsync("POST", $"{this._host}/data", request);

            if (0 == response[0])
            {
                byte infoVolume = 0;
                string infoSong = "";

                if (5 != e)
                {
                    infoVolume = response[3];
                }

                if (3 == response[1])
                {
                    infoSong = "Audio zone";
                }
                else
                {
                    var tt = response[2];
                    if (11 == e)
                    {
                        if (0 == tt)
                            tt = (byte)(stations_count - 1);
                        else
                            tt -= 1;

                        if (stations_count > response[2])
                            infoSong = station_name[tt];
                    }
                    else
                    {
                        if (10 == e)
                        {
                            if (tt == stations_count - 1)
                                tt = 0;
                            else
                                tt += 1;

                            if (stations_count > response[2])
                                infoSong = station_name[tt];
                        }
                        else
                        {
                            if (stations_count > response[2])
                                infoSong = station_name[response[2]];
                        }
                    }
                }

                if (3 == e)
                {
                    is_playing = true;
                }
                else if (4 == e)
                {
                    is_playing = false;
                }

                if (9 == e)
                {
                    if (audio_mute == 0)
                        audio_mute = 1;
                    else
                        audio_mute = 0;
                }
                else
                {
                    if (response[4] == 1)
                        audio_mute = 1;
                    else
                        audio_mute = 0;
                }

                return new LaraOpenResult()
                {
                    IsMute = audio_mute != 0,
                    IsPlaying = is_playing,
                    Song = infoSong,
                    Volume = infoVolume
                };
            }
            else
            {
                throw new Exception("Unknown error");
            }
        }

        public Task GetStationPageAsync()
        {
            throw new NotImplementedException();
        }

        public Task SetStationPageAsync()
        {
            throw new NotImplementedException();
        }

        public Task GetEq5Async()
        {
            throw new NotImplementedException();
        }

        public Task SetEq5Async()
        {
            throw new NotImplementedException();
        }

        private async Task<byte[]> MakeRequestAsync(string method, string uri, byte[] msg = null)
        {
            using (HttpRequestMessage request = new HttpRequestMessage())
            {
                request.Method = new HttpMethod(method);
                request.RequestUri = new Uri(uri);

                if (msg != null)
                {
                    request.Content = new ByteArrayContent(msg);
                }

                // use previous WWW auth header if available
                if (!string.IsNullOrEmpty(_wwwAuth))
                {
                    request.Headers.Add("Authorization", DigestHelper.GetDigest(_wwwAuth, uri, method, this._userName, this._password));
                }

                using (HttpResponseMessage response = await _httpClient.SendAsync(request))
                {
                    // if we get unauthorized, the WWW-Authenticate header will contain new nonce
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        string wwwAuth = response.Headers.GetValues("WWW-Authenticate").Single();
                        if (wwwAuth.StartsWith("Digest ", StringComparison.OrdinalIgnoreCase))
                        {
                            // save the nonce for the next request
                            this._wwwAuth = wwwAuth;

                            // repeat the request once more with the new Authorization header
                            using (HttpRequestMessage repeatedRequest = new HttpRequestMessage())
                            {
                                repeatedRequest.Method = new HttpMethod(method);
                                repeatedRequest.RequestUri = new Uri(uri);
                                repeatedRequest.Headers.Add("Authorization", DigestHelper.GetDigest(wwwAuth, uri, method, this._userName, this._password));

                                using (HttpResponseMessage repeatedResponse = await _httpClient.SendAsync(repeatedRequest))
                                {
                                    repeatedResponse.EnsureSuccessStatusCode();
                                    return await repeatedResponse.Content.ReadAsByteArrayAsync();
                                }
                            }
                        }
                        else
                        {
                            throw new NotSupportedException(wwwAuth);
                        }
                    }

                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsByteArrayAsync();
                }
            }
        }

        private static string ParseString(byte[] data, int index)
        {
            return _encoding.GetString(data.Skip(index).Take(MAX_SETTINGS_STRING).ToArray()).TrimEnd('\0');
        }

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (this._httpClient != null)
                    {
                        this._httpClient.Dispose();
                    }
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion // IDisposable
    }
}
