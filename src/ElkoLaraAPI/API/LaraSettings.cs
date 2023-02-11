namespace ElkoLaraAPI.API
{
    /// <summary>
    /// Radio configuration.
    /// </summary>
    public class LaraSettings
    {
        public byte[] ControlBits { get; set; }
        public byte IrLockBits { get; set; }
        public string IpAddress { get; set; }
        public string IpMask { get; set; }
        public string IpGateway { get; set; }
        public byte WatchdogPeriod { get; set; }
        public byte WatchdogCount { get; set; }
        public string IpAz { get; set; }
        public string IpSntp { get; set; }
        public string IpDns { get; set; }
        public byte Language { get; set; }
        public byte Ringtone { get; set; }
        public byte RingVolume { get; set; }
        public byte CallVolume { get; set; }
        public byte MicGain { get; set; }
        public byte Utc { get; set; }
        public string DeviceName { get; set; }
        public string AdminLogin { get; set; }
        public string AdminPassword { get; set; }
        public string UserLogin { get; set; }
        public string UserPassword { get; set; }
        public string AudioZoneUserName { get; set; }
        public string AudioZonePassword { get; set; }   
    }
}
