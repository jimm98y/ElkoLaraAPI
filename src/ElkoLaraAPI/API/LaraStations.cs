using System;
namespace ElkoLaraAPI.API
{
	public class LaraStations
	{
        public const byte MAX_STATIONS = 40;

        public byte StationsCount = 0;
        public string[] StationName = new string[MAX_STATIONS];
        public string[] StationDomainName = new string[MAX_STATIONS];
        public string[] StationFileName = new string[MAX_STATIONS];
        public byte[] StationIp0 = new byte[MAX_STATIONS];
        public byte[] StationIp1 = new byte[MAX_STATIONS];
        public byte[] StationIp2 = new byte[MAX_STATIONS];
        public byte[] StationIp3 = new byte[MAX_STATIONS];
        public int[] StationPort = new int[MAX_STATIONS];
    }
}
