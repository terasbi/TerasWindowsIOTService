namespace LCS_IoT_Svc
{
    using System;
    using System.Configuration;    
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client.Exceptions;

    public static class IoTHubManager
    {
        private static RegistryManager RegistryManager { get; set; }

        public static void SaveConnectionString(string hostName, string deviceId, string accessKey)
        {
            var config = ConfigurationManager.OpenExeConfiguration(System.Reflection.Assembly.GetEntryAssembly().Location);
            config.AppSettings.Settings["DeviceKey"].Value = StringSec.Encrypt(accessKey);
            config.Save(ConfigurationSaveMode.Full);
            ConfigurationManager.RefreshSection("appSettings");
        }

        public static void SaveConnMode(string strKeyMode, bool bInit)
        {
            var config = ConfigurationManager.OpenExeConfiguration(System.Reflection.Assembly.GetEntryAssembly().Location);
            config.AppSettings.Settings["KeyMode"].Value = strKeyMode;
            config.Save(ConfigurationSaveMode.Full);
            ConfigurationManager.RefreshSection("appSettings");
        }

        public static void GenKey(string connectionString, string deviceId)
        {
            try
            {
                RegistryManager = RegistryManager.CreateFromConnectionString(connectionString);
                GetDeviceAsync(deviceId).Wait();
            }
            catch (UnauthorizedException ioTEx)
            {
                Logger.WrtLogErr("Unauthorized to Gen Key - " + ioTEx.Message);
                throw;
            }
        }

        public static void CreateNewKey()
        {
            NewKey.PriKey = Base64Encode(Guid.NewGuid().ToString());
            NewKey.SecKey = Base64Encode(Guid.NewGuid().ToString());
        }

        public static byte[] Base64Encode(string plainText) 
        {
            var hash = new HMACSHA256(Encoding.ASCII.GetBytes("thor"));
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return hash.ComputeHash(plainTextBytes);
        }

        private static async Task GetDeviceAsync(string deviceId)
        {
            Device device;
            try
            {
                device = await RegistryManager.GetDeviceAsync(deviceId);
                SaveConnectionString(Program.StrIotHubConnString.Split(';')[0].Substring(9), deviceId, device.Authentication.SymmetricKey.PrimaryKey);
                SaveConnectionString(Program.StrIotHubConnString.Split(';')[0].Substring(9), deviceId, device.Authentication.SymmetricKey.SecondaryKey);
            }
            catch (DeviceNotFoundException dnfEx)
            {
                AppInsightsManager.TrackException(dnfEx);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("ErrorCode:DeviceNotFound"))
                {
                    AppInsightsManager.TrackException(ex);
                }
            }
        }
    }

    public static class NewKey
    {
        public static byte[] PriKey { get; set; }

        public static byte[] SecKey { get; set; }
    }
}