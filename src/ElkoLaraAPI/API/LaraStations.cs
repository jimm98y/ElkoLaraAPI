namespace ElkoLaraAPI.API
{
    /// <summary>
    /// Configured stations.
    /// </summary>
	public class LaraStations
	{
        /// <summary>
        /// Maximum supported number of stations.
        /// </summary>
        public const byte MAX_STATIONS = 40;

        /// <summary>
        /// Actual number of stations.
        /// </summary>
        public byte Count = 0;

        public string[] Name = new string[MAX_STATIONS];
        public string[] DomainName = new string[MAX_STATIONS];
        public string[] FileName = new string[MAX_STATIONS];
        public byte[] Ip0 = new byte[MAX_STATIONS];
        public byte[] Ip1 = new byte[MAX_STATIONS];
        public byte[] Ip2 = new byte[MAX_STATIONS];
        public byte[] Ip3 = new byte[MAX_STATIONS];
        public int[] Port = new int[MAX_STATIONS];
    }
}
