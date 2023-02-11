namespace ElkoLaraAPI.API
{
    /// <summary>
    /// Basic information about the smart radio.
    /// </summary>
    public class LaraBasicInfo
    {
        /// <summary>
        /// Firmware version.
        /// </summary>
        public string Firmware { get; set; }

        /// <summary>
        /// Version of the hardware.
        /// </summary>
        public string Hardware { get; set; }

        /// <summary>
        /// IP address of the radio.
        /// </summary>
        public string IpAddress { get; set; }

        /// <summary>
        /// Name of the device.
        /// </summary>
        public string Name { get; set; }
    }
}
