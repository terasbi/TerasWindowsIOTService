namespace WindowsIoTServices.Messages
{
    using Newtonsoft.Json;

    /// <summary>   
    /// Message structure for IOT hub message
    /// </summary>
    public class IoTHubMessages
    {
        /// <summary>
        /// Gets or sets the row.
        /// </summary>
        /// <value>
        /// The row.
        /// </value>
        [JsonProperty("row")]
        public string Row { get; set; }
    }

    public class IoTHubHeartBeatMessages
    {
        [JsonProperty("Status")]
        public string Status { get; set; }

        [JsonProperty("PlazaNo")]
        public string PlzNo { get; set; }

        [JsonProperty("LaneNo")]
        public string LaneNo { get; set; }

        [JsonProperty("TimeStamp")]
        public string DtTimestamp { get; set; }

        [JsonProperty("Version")]
        public string Version { get; set; }
    }

    public class IotHubMessageTrxCounter
    {
        [JsonProperty("PlazaNo")]
        public string PlazaNo { get; set; }

        [JsonProperty("LaneNo")]
        public string LaneNo { get; set; }

        [JsonProperty("SeqHour")]
        public string SeqHour { get; set; }

        [JsonProperty("TransactionDate")]
        public string TransactionDate { get; set; }

        [JsonProperty("TransactionCount")]
        public string TransactionCount { get; set; }
    }
}
