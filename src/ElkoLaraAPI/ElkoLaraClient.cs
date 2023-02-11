using ElkoLaraAPI.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ElkoLaraAPI
{
    public class ElkoLaraClient
    {
        private const int MAX_SETTINGS_STRING = 16;
        private static readonly Encoding _encoding = null;

        private readonly string _host;
        private readonly string _userName;
        private readonly string _password;

        private readonly Random _rand = new Random();
        private string _wwwAuth;

        static ElkoLaraClient()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _encoding = Encoding.GetEncoding(1250);
        }

        public ElkoLaraClient(string ipAddress, string userName, string password)
        {
            this._host = ipAddress ?? throw new ArgumentNullException(nameof(ipAddress));
            this._userName = userName ?? throw new ArgumentNullException(nameof(userName));
            this._password = password ?? throw new ArgumentNullException(nameof(password));
        }

        private string GetUri(string path = "")
        {
            return $"http://{this._host}{path}";
        }

        public async Task<string> GetIndexPageAsync()
        {
            var response = await MakeRequestAsync("GET", GetUri());
            return Encoding.UTF8.GetString(response.Content);
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

            var httpResponse = await MakeRequestAsync("POST", GetUri("/data"), request);
            byte[] response = httpResponse.Content;

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

        /// <summary>
        /// Returns the settings including user names and passwords.
        /// Vulnerability: This method does not require any authentication, so anybody can call it and just get the user name and password :)
        /// </summary>
        /// <returns></returns>
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

            var httpResponse = await MakeRequestAsync("POST", GetUri("/data"), request);
            byte[] response = httpResponse.Content;

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
        private string[] station_domain_name = new string[MAX_STATIONS];
        private string[] station_file_name = new string[MAX_STATIONS];
        private byte[] station_ip_0 = new byte[MAX_STATIONS];
        private byte[] station_ip_1 = new byte[MAX_STATIONS];
        private byte[] station_ip_2 = new byte[MAX_STATIONS];
        private byte[] station_ip_3 = new byte[MAX_STATIONS];
        private int[] station_port = new int[MAX_STATIONS];

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

            var httpResponse = await MakeRequestAsync("POST", GetUri("/data"), request);
            byte[] response = httpResponse.Content;

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


        public async Task GetStationPageAsync(int page = 0)
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
                0 // will be replaced
            };

            switch(page)
            {
                case 0:
                    request[9] = 6;
                    break;

                case 1:
                    request[9] = 12;
                    break;

                case 2:
                    request[9] = 13;
                    break;

                case 3:
                    request[9] = 14;
                    break;

                default:
                    throw new NotSupportedException($"Page not supported: {page}");
            }

            var httpResponse = await MakeRequestAsync("POST", GetUri("/data"), request);
            byte[] response = httpResponse.Content;

            stations_count = response[12];

            for (int i = 0; i < 10; i++)
            {
                for (int n = 0; n < MAX_NAME_STRING + 1; n++)
                {
                    station_name[i + 10 * page] = "";
                    char k;
                    if ('\0' == (k = _encoding.GetString(new byte[] { response[13 + n + 139 * i] }).ToCharArray()[0]))
                        break;
                    station_name[i + 10 * page] += k;
                }

                for (int n = 0; n < MAX_NAME_STRING + 1; n++)
                {
                    station_domain_name[i + 10 * page] = "";
                    char k;
                    if ('\0' == (k = _encoding.GetString(new byte[] { response[26 + n + 139 * i] }).ToCharArray()[0]))
                        break;
                    station_domain_name[i + 10 * page] += k;
                }

                for (int n = 0; n < MAX_NAME_STRING + 1; n++)
                {
                    station_file_name[i + 10 * page] = "";
                    char k;
                    if ('\0' == (k = _encoding.GetString(new byte[] { response[76 + n + 139 * i] }).ToCharArray()[0]))
                        break;
                    station_file_name[i + 10 * page] += k;
                }

                station_ip_0[i + 10 * page] = response[146 + 139 * i];
                station_ip_1[i + 10 * page] = response[147 + 139 * i];
                station_ip_2[i + 10 * page] = response[148 + 139 * i];
                station_ip_3[i + 10 * page] = response[149 + 139 * i];
                station_port[i + 10 * page] = response[150 + 139 * i] << 8 | 255 & response[151 + 139 * i];
            }
        }

        public Task SetStationPageAsync()
        {
            throw new NotImplementedException();
        }

        // TODO encapsulate
        const int eqMAX = 5;
        int eqEditMode = 0;
        int _eqSelected = 0;
        int[] eqLvl1 = new int[eqMAX];
        int[] eqLvl2 = new int[eqMAX];
        int[] eqLvl3 = new int[eqMAX];
        int[] eqLvl4 = new int[eqMAX];
        int[] eqLvl5 = new int[eqMAX];
        int[] eqFreq1 = new int[eqMAX];
        int[] eqFreq2 = new int[eqMAX];
        int[] eqFreq3 = new int[eqMAX];
        int[] eqFreq4 = new int[eqMAX];

        public async Task GetEq5Async()
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
                48
            };

            var httpResponse = await MakeRequestAsync("POST", GetUri("/data"), request);
            byte[] response = httpResponse.Content;

            var t = 0;
            for (t = 0; t < eqMAX; t++)
            {
                eqLvl1[t] = (response[11 + 18 * t] << 8 | response[12 + 18 * t]) << 16 >> 16;
                eqLvl2[t] = (response[13 + 18 * t] << 8 | response[14 + 18 * t]) << 16 >> 16;
                eqLvl3[t] = (response[15 + 18 * t] << 8 | response[16 + 18 * t]) << 16 >> 16;
                eqLvl4[t] = (response[17 + 18 * t] << 8 | response[18 + 18 * t]) << 16 >> 16;
                eqLvl5[t] = (response[19 + 18 * t] << 8 | response[20 + 18 * t]) << 16 >> 16;
                eqFreq1[t] = response[21 + 18 * t] << 8 | response[22 + 18 * t];
                eqFreq2[t] = response[23 + 18 * t] << 8 | response[24 + 18 * t];
                eqFreq3[t] = response[25 + 18 * t] << 8 | response[26 + 18 * t];
                eqFreq4[t] = response[27 + 18 * t] << 8 | response[28 + 18 * t];
            }
        }

        public Task SetEq5Async()
        {
            throw new NotImplementedException();
        }

        

        /// <summary>
        /// Sends the request to Elko Lara and returns the response.
        /// </summary>
        /// <param name="method"></param>
        /// <param name="uri"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="ArgumentException"></exception>
        private async Task<CustomHttpResponse> MakeRequestAsync(string method, string uri, byte[] msg = null)
        {
            if (!(method == "POST" || method == "GET"))
                throw new NotSupportedException($"Method not supported: {method}");

            if (!Uri.IsWellFormedUriString(uri, UriKind.Absolute))
                throw new ArgumentException("Invalid URI.");

            // Here we cannot use the standard .NET HttpClient, because it splits the POST request into 2 TCP packets: the headers are sent in the first one, while the content is sent in another one.
            // It looks like Elko Lara cannot reassemlbe the request correctly and therefore once we attempt to write anything we get 404 Not found from the /Data endpoint. There seems to be no way of
            //  changing this behavior in .NET HttpClient. This issue is reproducible on MacOSX 13.2 with netstandard 2.0 DLL under NET7 MAUI host. The solution is to use TcpClient instead.
            Dictionary<string, string> headers = null;

            // use previous WWW auth header if available
            if (!string.IsNullOrEmpty(this._wwwAuth))
            {
                headers = new Dictionary<string, string>();
                headers.Add("Authorization", DigestHelper.GetDigest(this._wwwAuth, uri, method, this._userName, this._password));
            }

            var httpResponse = await CustomHttpClient.MakeRequestAsync(method, uri, headers, msg);
            
            if(httpResponse.Headers != null && httpResponse.Headers.ContainsKey("WWW-Authenticate"))
            {
                string wwwAuth = httpResponse.Headers["WWW-Authenticate"];

                headers = new Dictionary<string, string>();
                headers["Authorization"] = DigestHelper.GetDigest(wwwAuth, uri, method, this._userName, this._password);

                // retry with the authenticate header
                httpResponse = await CustomHttpClient.MakeRequestAsync(method, uri, headers, msg);

                this._wwwAuth = wwwAuth;
            }

            return httpResponse;
        }

        private static string ParseString(byte[] data, int index)
        {
            return _encoding.GetString(data.Skip(index).Take(MAX_SETTINGS_STRING).ToArray()).TrimEnd('\0');
        }
    }
}
