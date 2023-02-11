using ElkoLaraAPI.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;

namespace ElkoLaraAPI
{
    /// <summary>
    /// Client for the Elko Lara smart radio https://www.elkoep.cz/lara.
    /// </summary>
    public class ElkoLaraClient
    {
        private const int MAX_SETTINGS_STRING = 16;
        private const byte MAX_NAME_STRING = 12;
        private const byte MAX_DOMAIN_NAME_STRING = 49;
        private const byte MAX_FILE_NAME_STRING = 69;

        private static readonly Encoding _encoding = null;

        private readonly string _host;
        private readonly string _userName;
        private readonly string _password;

        private readonly Random _rand = new Random();
        private string _wwwAuth;

        static ElkoLaraClient()
        {
            // has to be called once per process to register the Windows 1250 encoding provider
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _encoding = Encoding.GetEncoding(1250);
        }

        /// <summary>
        /// Ctor.
        /// </summary>
        /// <param name="ipAddress">IP address of the radio.</param>
        /// <param name="userName">User name.</param>
        /// <param name="password">Password.</param>
        /// <exception cref="ArgumentNullException"></exception>
        public ElkoLaraClient(string ipAddress, string userName, string password)
        {
            this._host = ipAddress ?? throw new ArgumentNullException(nameof(ipAddress));
            this._userName = userName ?? throw new ArgumentNullException(nameof(userName));
            this._password = password ?? throw new ArgumentNullException(nameof(password));
        }

        /// <summary>
        /// Sign in.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> SignInAsync()
        {
            // Sign in makes a request to the default endpoint and retrieves the HTML of the landing page.
            //  Only this endpoint returns the WWW-Authenticate header with the challenge needed for Digest.
            var response = await MakeRequestAsync("GET", GetUri());
            return response.StatusCode == 200;
        }

        /// <summary>
        /// Start playing.
        /// </summary>
        /// <param name="stations">Optional. If not provided, the result will not contain the song/radio station.</param>
        /// <returns><see cref="LaraOpenResult"/>.</returns>
        public Task<LaraOpenResult> PlayAsync(LaraStations stations = null)
        {
            return OpenRemoteAsync(3, 0, stations);
        }

        /// <summary>
        /// Stop playing.
        /// </summary>
        /// <param name="stations">Optional. If not provided, the result will not contain the song/radio station.</param>
        /// <returns><see cref="LaraOpenResult"/>.</returns>
        public Task<LaraOpenResult> StopAsync(LaraStations stations = null)
        {
            return OpenRemoteAsync(4, 0, stations);
        }

        /// <summary>
        /// Set volume.
        /// </summary>
        /// <param name="volume">Volume in the range of 1 - 100.</param>
        /// <param name="stations">Optional. If not provided, the result will not contain the song/radio station.</param>
        /// <returns><see cref="LaraOpenResult"/>.</returns>
        public Task<LaraOpenResult> SetVolumeAsync(int volume, LaraStations stations = null)
        {
            if (volume < 0 || volume > 100)
                throw new ArgumentOutOfRangeException(nameof(volume));

            return OpenRemoteAsync(5, (byte)volume, stations);
        }

        /// <summary>
        /// Go to next station.
        /// </summary>
        /// <param name="stations">Optional. If not provided, the result will not contain the song/radio station.</param>
        /// <returns><see cref="LaraOpenResult"/>.</returns>
        public Task<LaraOpenResult> NextStationAsync(LaraStations stations = null)
        {
            return OpenRemoteAsync(0x0A, 0, stations);
        }

        /// <summary>
        /// Go to previous station.
        /// </summary>
        /// <param name="stations">Optional. If not provided, the result will not contain the song/radio station.</param>
        /// <returns><see cref="LaraOpenResult"/>.</returns>
        public Task<LaraOpenResult> PreviousStationAsync(LaraStations stations = null)
        {
            return OpenRemoteAsync(0x0B, 0, stations);
        }

        /// <summary>
        /// Returns basic information about the Elko Lara such as the name and HW and FW verisons.
        /// </summary>
        /// <returns><see cref="LaraBasicInfo"/>.</returns>
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

            var httpResponse = await MakeRequestAsync("POST", GetDataUri(), request);
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

        /// <summary>
        /// Returns the settings including user names and passwords.
        /// Vulnerability: This method does not require any authentication, so anybody can call it and just get the user name and password :)
        /// </summary>
        /// <returns><see cref="LaraSettings"/>.</returns>
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

            var httpResponse = await MakeRequestAsync("POST", GetDataUri(), request);
            byte[] response = httpResponse.Content;

            byte[] controlBits = new byte[3] { response[11], response[12], response[95] };
            byte irLockBits = response[96];
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
                IrLockBits = irLockBits,
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

        /// <summary>
        /// Changes the settings.
        /// </summary>
        /// <param name="settings"><see cref="LaraSettings"/>.</param>
        /// <returns>true if successful, false otherwise.</returns>
        /// <exception cref="UnauthorizedAccessException"></exception>
        public async Task<bool> SetSettingsAsync(LaraSettings settings)
        {
            if (string.IsNullOrEmpty(_wwwAuth))
                throw new UnauthorizedAccessException($"User must be logged in! Call {nameof(SignInAsync)} to sign in.");

            byte[] request = new byte[250];
            request[0] = 255;
            request[1] = 250;
            request[2] = 250;
            request[3] = 255;
            request[4] = (byte)Math.Floor(125 * _rand.NextDouble() + 1);
            request[5] = (byte)Math.Floor(125 * _rand.NextDouble() + 125 + 1);
            request[6] = 0;
            request[7] = 128;
            request[8] = 192;
            request[9] = 4;
            request[45] = settings.ControlBits[0];
            request[46] = settings.ControlBits[1];

            var i = 0;
            for (i = 0; i < MAX_SETTINGS_STRING + 1; i++)
            {
                request[47 + i] = 0;
                request[64 + i] = 0;
                request[81 + i] = 0;
                request[98 + i] = 0;
                request[135 + i] = 0;
                request[166 + i] = 0;
                request[183 + i] = 0;
            }

            byte[] byteString;

            byteString = _encoding.GetBytes(settings.AdminLogin);
            for (i = 0; i < CheckStringLength(byteString, MAX_SETTINGS_STRING); i++)
                request[47 + i] = byteString[i];

            byteString = _encoding.GetBytes(settings.AdminPassword);
            for (i = 0; i < CheckStringLength(byteString, MAX_SETTINGS_STRING); i++)
                request[64 + i] = byteString[i];

            byteString = _encoding.GetBytes(settings.UserLogin);
            for (i = 0; i < CheckStringLength(byteString, MAX_SETTINGS_STRING); i++)
                request[81 + i] = byteString[i];

            byteString = _encoding.GetBytes(settings.UserPassword);
            for (i = 0; i < CheckStringLength(byteString, MAX_SETTINGS_STRING); i++)
                request[98 + i] = byteString[i];

            byteString = _encoding.GetBytes(settings.DeviceName);
            for (i = 0; i < CheckStringLength(byteString, MAX_SETTINGS_STRING); i++)
                request[135 + i] = byteString[i];

            byteString = _encoding.GetBytes(settings.AudioZoneUserName);
            for (i = 0; i < CheckStringLength(byteString, MAX_SETTINGS_STRING); i++)
                request[166 + i] = byteString[i];

            byteString = _encoding.GetBytes(settings.AudioZonePassword);
            for (i = 0; i < CheckStringLength(byteString, MAX_SETTINGS_STRING); i++)
                request[183 + i] = byteString[i];

            var settings_ip = System.Net.IPAddress.Parse(settings.IpAddress).GetAddressBytes();
            request[115] = settings_ip[0];
            request[116] = settings_ip[1];
            request[117] = settings_ip[2];
            request[118] = settings_ip[3];

            var settings_mask = System.Net.IPAddress.Parse(settings.IpMask).GetAddressBytes();
            request[119] = settings_mask[0];
            request[120] = settings_mask[1];
            request[121] = settings_mask[2];
            request[122] = settings_mask[3];

            var settings_gateway = System.Net.IPAddress.Parse(settings.IpGateway).GetAddressBytes();
            request[123] = settings_gateway[0];
            request[124] = settings_gateway[1];
            request[125] = settings_gateway[2];
            request[126] = settings_gateway[3];

            request[127] = settings.WatchdogPeriod;
            request[128] = settings.WatchdogCount;
            request[129] = settings.ControlBits[2];
            request[130] = settings.IrLockBits;

            var settings_az_ip = System.Net.IPAddress.Parse(settings.IpAz).GetAddressBytes();
            request[131] = settings_az_ip[0];
            request[132] = settings_az_ip[1];
            request[133] = settings_az_ip[2];
            request[134] = settings_az_ip[3];

            var settings_sntp_ip = System.Net.IPAddress.Parse(settings.IpSntp).GetAddressBytes();
            request[152] = settings_sntp_ip[0];
            request[153] = settings_sntp_ip[1];
            request[154] = settings_sntp_ip[2];
            request[155] = settings_sntp_ip[3];

            request[156] = (byte)(settings.Utc < 0 ? settings.Utc + 255 + 1 : settings.Utc);

            var settings_dns_ip = System.Net.IPAddress.Parse(settings.IpDns).GetAddressBytes();
            request[157] = settings_dns_ip[0];
            request[158] = settings_dns_ip[1];
            request[159] = settings_dns_ip[2];
            request[160] = settings_dns_ip[3];

            request[161] = settings.Language;
            request[162] = settings.Ringtone;
            request[163] = settings.RingVolume;
            request[164] = settings.CallVolume;
            request[165] = settings.MicGain;

            var httpResponse = await MakeRequestAsync("POST", GetDataUri(), request);
            return httpResponse.StatusCode == 200;
        }

        /// <summary>
        /// Returns the statons page.
        /// </summary>
        /// <param name="page">Page number.</param>
        /// <returns><see cref="LaraStations"/>.</returns>
        /// <exception cref="NotSupportedException"></exception>
        public async Task<LaraStations> GetStationPageAsync(int page = 0)
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

            var httpResponse = await MakeRequestAsync("POST", GetDataUri(), request);
            byte[] response = httpResponse.Content;

            var result = new LaraStations();

            result.Count = response[12];

            for (int i = 0; i < 10; i++)
            {
                for (int n = 0; n < MAX_NAME_STRING + 1; n++)
                {
                    result.Name[i + 10 * page] = "";
                    char k;
                    if (0 == (k = _encoding.GetString(new byte[] { response[13 + n + 139 * i] }).ToCharArray()[0]))
                        break;
                    result.Name[i + 10 * page] += k;
                }

                for (int n = 0; n < MAX_NAME_STRING + 1; n++)
                {
                    result.DomainName[i + 10 * page] = "";
                    char k;
                    if (0 == (k = _encoding.GetString(new byte[] { response[26 + n + 139 * i] }).ToCharArray()[0]))
                        break;
                    result.DomainName[i + 10 * page] += k;
                }

                for (int n = 0; n < MAX_NAME_STRING + 1; n++)
                {
                    result.FileName[i + 10 * page] = "";
                    char k;
                    if (0 == (k = _encoding.GetString(new byte[] { response[76 + n + 139 * i] }).ToCharArray()[0]))
                        break;
                    result.FileName[i + 10 * page] += k;
                }

                result.Ip0[i + 10 * page] = response[146 + 139 * i];
                result.Ip1[i + 10 * page] = response[147 + 139 * i];
                result.Ip2[i + 10 * page] = response[148 + 139 * i];
                result.Ip3[i + 10 * page] = response[149 + 139 * i];
                result.Port[i + 10 * page] = response[150 + 139 * i] << 8 | 255 & response[151 + 139 * i];
            }

            return result;
        }

        /// <summary>
        /// Sets the stations page.
        /// </summary>
        /// <param name="stations"><see cref="LaraStations"/>.</param>
        /// <param name="page">Page number.</param>
        /// <returns>true if successful, false otherwise.</returns>
        /// <exception cref="UnauthorizedAccessException"></exception>
        public async Task<bool> SetStationPageAsync(LaraStations stations, int page = 0)
        {
            if (string.IsNullOrEmpty(_wwwAuth))
                throw new UnauthorizedAccessException($"User must be logged in! Call {nameof(SignInAsync)} to sign in.");

            var request = new byte[1450];
            request[0] = 255;
            request[1] = 250;
            request[2] = 250;
            request[3] = 255;
            request[4] = (byte)Math.Floor(125 * _rand.NextDouble() + 1);
            request[5] = (byte)Math.Floor(125 * _rand.NextDouble() + 125 + 1);
            request[6] = 0;
            request[7] = 128;
            request[8] = 192;
            request[9] = 8;
            request[45] = (byte)page;
            request[46] = stations.Count;

            var n = 0;
            var i = 0;
            for (n = 0; n < 10; n++)
            {
                byte[] byteString;

                for (i = 0; i < MAX_NAME_STRING + 1; i++)
                    request[47 + i + 139 * n] = 0;
                byteString = _encoding.GetBytes(stations.Name[n + 10 * page]);
                for (i = 0; i < CheckStringLength(byteString, MAX_NAME_STRING); i++)
                    request[47 + i + 139 * n] = byteString[i];

                for (i = 0; i < MAX_DOMAIN_NAME_STRING + 1; i++)
                    request[60 + i + 139 * n] = 0;
                byteString = _encoding.GetBytes(stations.DomainName[n + 10 * page]);
                for (i = 0; i < CheckStringLength(byteString, MAX_DOMAIN_NAME_STRING); i++)
                    request[60 + i + 139 * n] = byteString[i];

                for (i = 0; i < MAX_FILE_NAME_STRING + 1; i++)
                    request[110 + i + 139 * n] = 0;
                byteString = _encoding.GetBytes(stations.FileName[n + 10 * page]);
                for (i = 0; i < CheckStringLength(byteString, MAX_FILE_NAME_STRING); i++)
                    request[120 + i + 139 * n] = byteString[i];

                request[180 + 139 * n] = stations.Ip0[n + 10 * page];
                request[181 + 139 * n] = stations.Ip1[n + 10 * page];
                request[182 + 139 * n] = stations.Ip2[n + 10 * page];
                request[183 + 139 * n] = stations.Ip3[n + 10 * page];
                request[184 + 139 * n] = (byte)(stations.Port[n + 10 * page] >> 8);
                request[185 + 139 * n] = (byte)(255 & stations.Port[n + 10 * page]);
            }

            var httpResponse = await MakeRequestAsync("POST", GetDataUri(), request);
            return httpResponse.StatusCode == 200;
        }

        /// <summary>
        /// Gets the equalizer configuration.
        /// </summary>
        /// <returns><see cref="LaraEq"/>.</returns>
        public async Task<LaraEq> GetEq5Async()
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

            var httpResponse = await MakeRequestAsync("POST", GetDataUri(), request);
            byte[] response = httpResponse.Content;

            var result = new LaraEq();

            var t = 0;
            for (t = 0; t < LaraEq.EQ_MAX; t++)
            {
                result.Lvl1[t] = (response[11 + 18 * t] << 8 | response[12 + 18 * t]) << 16 >> 16;
                result.Lvl2[t] = (response[13 + 18 * t] << 8 | response[14 + 18 * t]) << 16 >> 16;
                result.Lvl3[t] = (response[15 + 18 * t] << 8 | response[16 + 18 * t]) << 16 >> 16;
                result.Lvl4[t] = (response[17 + 18 * t] << 8 | response[18 + 18 * t]) << 16 >> 16;
                result.Lvl5[t] = (response[19 + 18 * t] << 8 | response[20 + 18 * t]) << 16 >> 16;
                result.Freq1[t] = response[21 + 18 * t] << 8 | response[22 + 18 * t];
                result.Freq2[t] = response[23 + 18 * t] << 8 | response[24 + 18 * t];
                result.Freq3[t] = response[25 + 18 * t] << 8 | response[26 + 18 * t];
                result.Freq4[t] = response[27 + 18 * t] << 8 | response[28 + 18 * t];
            }

            return result;
        }

        /// <summary>
        /// Sets the equalizer.
        /// </summary>
        /// <param name="eq"><see cref="LaraEq"/>.</param>
        /// <returns>true if successful, false otherwise.</returns>
        /// <exception cref="UnauthorizedAccessException"></exception>
        public async Task<bool> SetEq5Async(LaraEq eq)
        {
            if (string.IsNullOrEmpty(_wwwAuth))
                throw new UnauthorizedAccessException($"User must be logged in! Call {nameof(SignInAsync)} to sign in.");

            var request = new byte[250];
            request[0] = 255;
            request[1] = 250;
            request[2] = 250;
            request[3] = 255;
            request[4] = (byte)Math.Floor(125 * _rand.NextDouble() + 1);
            request[5] = (byte)Math.Floor(125 * _rand.NextDouble() + 125 + 1);
            request[6] = 0;
            request[7] = 128;
            request[8] = 192;
            request[9] = 50;

            int i = 0;
            for (i = 0; i < LaraEq.EQ_MAX; i++)
            {
                request[45 + 18 * i] = (byte)(eq.Lvl1[i] >> 8 & 255);
                request[46 + 18 * i] = (byte)(255 & eq.Lvl1[i]);
                request[47 + 18 * i] = (byte)(eq.Lvl2[i] >> 8 & 255);
                request[48 + 18 * i] = (byte)(255 & eq.Lvl2[i]);
                request[49 + 18 * i] = (byte)(eq.Lvl3[i] >> 8 & 255);
                request[50 + 18 * i] = (byte)(255 & eq.Lvl3[i]);
                request[51 + 18 * i] = (byte)(eq.Lvl4[i] >> 8 & 255);
                request[52 + 18 * i] = (byte)(255 & eq.Lvl4[i]);
                request[53 + 18 * i] = (byte)(eq.Lvl5[i] >> 8 & 255);
                request[54 + 18 * i] = (byte)(255 & eq.Lvl5[i]);
                request[55 + 18 * i] = (byte)(eq.Freq1[i] >> 8 & 255);
                request[56 + 18 * i] = (byte)(255 & eq.Freq1[i]);
                request[57 + 18 * i] = (byte)(eq.Freq2[i] >> 8 & 255);
                request[58 + 18 * i] = (byte)(255 & eq.Freq2[i]);
                request[59 + 18 * i] = (byte)(eq.Freq3[i] >> 8 & 255);
                request[60 + 18 * i] = (byte)(255 & eq.Freq3[i]);
                request[61 + 18 * i] = (byte)(eq.Freq4[i] >> 8 & 255);
                request[62 + 18 * i] = (byte)(255 & eq.Freq4[i]);
            };

            var httpResponse = await MakeRequestAsync("POST", GetDataUri(), request);
            return httpResponse.StatusCode == 200;
        }

        /// <summary>
        /// Get the intercom.
        /// </summary>
        /// <returns><see cref="LaraIntercom"/>.</returns>
        /// <exception cref="NotImplementedException"></exception>
        public Task<LaraIntercom> GetIntercomAsync()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Set the intercom.
        /// </summary>
        /// <param name="eq"><see cref="LaraIntercom"/>.</param>
        /// <returns>true if successful, false otherwise.</returns>
        /// <exception cref="NotImplementedException"></exception>
        public Task<bool> SetIntercomAsync(LaraIntercom eq)
        {
            throw new NotImplementedException();
        }

        private async Task<LaraOpenResult> OpenRemoteAsync(byte e, byte t, LaraStations stations = null)
        {
            if (string.IsNullOrEmpty(_wwwAuth))
                throw new UnauthorizedAccessException($"User must be logged in! Call {nameof(SignInAsync)} to sign in.");

            byte[] request = new byte[]
            {
                255,
                251,
                251,
                204,
                e,
                t
            };

            var httpResponse = await MakeRequestAsync("POST", GetDataUri(), request);
            byte[] response = httpResponse.Content;
            var result = new LaraOpenResult();

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
                else if (stations != null) // stations are only resolved optionally
                {
                    var tt = response[2];
                    if (11 == e)
                    {
                        if (0 == tt)
                            tt = (byte)(stations.Count - 1);
                        else
                            tt -= 1;

                        if (stations.Count > response[2])
                            infoSong = stations.Name[tt];
                    }
                    else
                    {
                        if (10 == e)
                        {
                            if (tt == stations.Count - 1)
                                tt = 0;
                            else
                                tt += 1;

                            if (stations.Count > response[2])
                                infoSong = stations.Name[tt];
                        }
                        else
                        {
                            if (stations.Count > response[2])
                                infoSong = stations.Name[response[2]];
                        }
                    }
                }

                if (3 == e)
                {
                    result.IsPlaying = true;
                }
                else if (4 == e)
                {
                    result.IsPlaying = false;
                }

                if (9 == e)
                {
                    // Toggle
                    //if (audio_mute == 0)
                    //    audio_mute = 1;
                    //else
                    //    audio_mute = 0;

                    // the API cannot know the previous state
                    result.IsMuteToggle = true;
                }
                else
                {
                    if (response[4] == 1)
                        result.IsMute = true;
                    else
                        result.IsMute = false;
                }

                result.Song = infoSong;
                result.Volume = infoVolume;

                return result;
            }
            else
            {
                throw new Exception("Unknown error");
            }
        }

        private async Task<SimpleHttpClient.SimpleHttpResponse> MakeRequestAsync(string method, string uri, byte[] msg = null)
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

            var httpResponse = await SimpleHttpClient.MakeRequestAsync(method, uri, headers, msg);
            
            if(httpResponse.StatusCode != 200 && httpResponse.Headers != null && httpResponse.Headers.ContainsKey("WWW-Authenticate"))
            {
                this._wwwAuth = httpResponse.Headers["WWW-Authenticate"];

                headers = new Dictionary<string, string>();
                headers.Add("Authorization", DigestHelper.GetDigest(this._wwwAuth, uri, method, this._userName, this._password));

                // retry with the authenticate header
                httpResponse = await SimpleHttpClient.MakeRequestAsync(method, uri, headers, msg);
            }

            return httpResponse;
        }

        private static string ParseString(byte[] data, int index)
        {
            return _encoding.GetString(data.Skip(index).Take(MAX_SETTINGS_STRING).ToArray()).TrimEnd('\0');
        }

        private static int CheckStringLength(byte[] e, int t)
        {
            return e.Length > t ? t : e.Length;
        }

        private string GetUri(string path = "")
        {
            return $"http://{this._host}{path}";
        }

        private string GetDataUri()
        {
            return GetUri("/data");
        }
    }
}
