namespace LCS_IoT_Svc
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web;
    using Microsoft.Azure.Devices.Client;
    using Newtonsoft.Json;

    public class IoTHubConn
    {
        /// <summary>
        /// The device identifier
        /// </summary>
        private string deviceId;

        /// <summary>
        /// The device key
        /// </summary>
        private string deviceKey;

        /// <summary>
        /// The host name
        /// </summary>
        private string hostName;

        /// <summary>
        /// The device client
        /// </summary>
        private DeviceClient deviceClient;

        /// <summary>
        /// Gets or sets a value indicating whether this instance is initialized.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is initialized; otherwise, <c>false</c>.
        /// </value>
        public bool IsInitialized { get; set; }

        /// <summary>
        /// Connects the asynchronous.
        /// </summary>
        /// <param name="iotHubConnectionString">The IOT hub connection string.</param>
        /// <param name="deviceId">The device identifier.</param>
        /// <param name="deviceKey">The device key.</param>
        /// <returns>The Task</returns>
        public async Task ConnectAsync(string iotHubConnectionString, string deviceId, string deviceKey)
        {
            try
            {
                this.deviceId = deviceId;
                this.deviceKey = deviceKey;
                var iotHubProperties = iotHubConnectionString.Split(";".ToCharArray());

                // Get the host name
                var hostNameItem = iotHubProperties.SingleOrDefault(item => item.StartsWith("HostName"));
                if (hostNameItem != null)
                {
                    this.hostName = hostNameItem.Split("=".ToCharArray())[1];
                }

                var deviceConnection = this.GenerateDeviceConnectionString(deviceId, deviceKey);
                this.deviceClient = DeviceClient.CreateFromConnectionString(deviceConnection);
                await this.deviceClient.OpenAsync();
                this.IsInitialized = true;
            }
            catch (Exception ex)
            {
                this.IsInitialized = false;
                Program.Log.Error(ex.Message);
            }
        }

        /// <summary>
        /// Closes the client asynchronously.
        /// </summary>
        /// <returns>The Task</returns>
        public Task CloseAsync()
        {
            return this.deviceClient?.CloseAsync();
        }

        /// <summary>
        /// Sends the message asynchronously.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>The Task</returns>
        public Task SendMessageAsync(object message, string strType)
        {
            var serializedMessage = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message)));
            if (strType == "HB")
            {
                serializedMessage.Properties["Type"] = "heartbeat";
            }

            return this.deviceClient.SendEventAsync(serializedMessage);
        }

        /// <summary>
        /// Generates the connection string.
        /// </summary>
        /// <param name="device">The device identifier.</param>
        /// <param name="devicekey">The device key.</param>
        /// <returns>
        /// Device connection string
        /// </returns>
        /// Try
        /// //////////////////
        protected string GenerateDeviceConnectionString(string device, string devicekey)
        {
            var authenticationMethod = new DeviceAuthenticationWithRegistrySymmetricKey(device, devicekey);
            var builder = IotHubConnectionStringBuilder.Create(this.hostName, authenticationMethod);
            return builder.ToString();
        }

        private static string CreateToken(string resourceUri, string policykey, string policyname, int validity)
        {
            TimeSpan sinceEpoch = DateTime.UtcNow - new DateTime(1970, 1, 1);
            var week = 60 * 60 * 24 * validity;
            var expiry = Convert.ToString((int)sinceEpoch.TotalSeconds + week);
            string stringToSign = HttpUtility.UrlEncode(resourceUri) + "\n" + expiry;
            HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(policykey));
            var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
            var sasToken = string.Format(CultureInfo.InvariantCulture, "SharedAccessSignature sr={0}&sig={1}&se={2}&skn={3}", HttpUtility.UrlEncode(resourceUri), HttpUtility.UrlEncode(signature), expiry, policyname);
            return sasToken;
        }       
    }
}
