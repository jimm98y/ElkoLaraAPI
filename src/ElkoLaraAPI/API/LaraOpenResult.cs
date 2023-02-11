namespace ElkoLaraAPI.API
{
    /// <summary>
    /// Result of the performed action.
    /// </summary>
    public class LaraOpenResult
    {
        /// <summary>
        /// Current volume level. Range 1-100.
        /// </summary>
        public byte Volume { get; set; }

        /// <summary>
        /// Current song/radio station.
        /// </summary>
        public string Song { get; set; }

        /// <summary>
        /// Is the radio playing?
        /// </summary>
        public bool IsPlaying { get; set; }

        /// <summary>
        /// Is the radio muted?
        /// </summary>
        public bool IsMute { get; set; }

        /// <summary>
        /// Was mute toggled? (Radio can report a toggle action, but does not report the state. If this is true, then regardless of the IsMute value the receiver should toggle the state.)
        /// </summary>
        public bool IsMuteToggle { get; set; }
    }
}
