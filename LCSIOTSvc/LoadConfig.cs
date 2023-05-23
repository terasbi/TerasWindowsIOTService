namespace LCS_IoT_Svc
{
    public class LoadConfig
    {
        /// <summary>
        /// Gets or sets the IOT hub connection string.
        /// </summary>
        /// <value>
        /// The IOT hub connection string.
        /// </value>
        public string IotHubConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the device identifier.
        /// </summary>
        /// <value>
        /// The device identifier.
        /// </value>
        public string DeviceId { get; set; }

        /// <summary>
        /// Gets or sets the device key.
        /// </summary>
        /// <value>
        /// The device key.
        /// </value>
        public string DeviceKey { get; set; }

        /// <summary>
        /// Gets or sets the plaza no.
        /// </summary>
        /// <value>
        /// The plaza no.
        /// </value>
        public string PlazaNo { get; set; }

        /// <summary>
        /// Gets or sets the entry plaza no.
        /// </summary>
        /// <value>
        /// The entry plaza no.
        /// </value>
        public string EntryPlaza { get; set; }

        /// <summary>
        /// Gets or sets the lane no.
        /// </summary>
        /// <value>
        /// The lane no.
        /// </value>
        public string LaneNo { get; set; }
    }
}
