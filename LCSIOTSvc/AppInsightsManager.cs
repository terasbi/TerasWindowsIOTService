namespace LCS_IoT_Svc
{
    using System;
    using System.Diagnostics;
    using System.Reflection;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.Extensibility;

    public class AppInsightsManager 
    {
        public static TelemetryClient Client;

        public static  TelemetryClient GetAppInsightsClient()
        {
//            var config = new TelemetryConfiguration
//            {
//                InstrumentationKey = Program.StrInstrumentKey
//            };
//            config.TelemetryChannel = new Microsoft.ApplicationInsights.Channel.InMemoryChannel
//            {
//                DeveloperMode = Debugger.IsAttached
//            };
//#if DEBUG
//            config.TelemetryChannel.DeveloperMode = true;
//#endif
            Client = new TelemetryClient();
            Client.InstrumentationKey = Program.StrInstrumentKey;
            Client.Context.Component.Version = Assembly.GetEntryAssembly().GetName().Version.ToString();
            var newGuid = Guid.NewGuid().ToString();
            Client.Context.Session.Id = newGuid;
            Client.Context.User.Id = Program.StrDeviceId + "-" + Assembly.GetEntryAssembly().GetName().Name.ToString();
            Client.Context.Device.OperatingSystem = Environment.OSVersion.ToString();
            Client.Context.Operation.Id = newGuid;
            return Client;
        }

        public static void TrackException(Exception ex)
        {
            var telex = new Microsoft.ApplicationInsights.DataContracts.ExceptionTelemetry(ex)
            {
                SeverityLevel = Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Error
            };
            Client.TrackException(telex);
            Flush();
        }

        public static void TrackTrace(string strMsg)
        {
            var telex = new Microsoft.ApplicationInsights.DataContracts.TraceTelemetry(strMsg)
            {
                SeverityLevel = Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Information
            };
            Client.TrackTrace(telex);
            Flush();
        }

        public static void TrackTraceWarning(string strMsg)
        {
            var telex = new Microsoft.ApplicationInsights.DataContracts.TraceTelemetry(strMsg)
            {
                SeverityLevel = Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Warning
            };
            Client.TrackTrace(telex);
            Flush();
        }

        internal static void Flush()
        {
            Client.Flush();
        }
    }
}
