namespace WindowsServiceTrxCounter
{
    using System;
    using Microsoft.ApplicationInsights;
    using System.Reflection;
    using Microsoft.ApplicationInsights.Extensibility;
    using System.Diagnostics;

    public class AppInsightsManager 
    {
        public static TelemetryClient client;

        public static  TelemetryClient GetAppInsightsClient()
        {
//            var config = new TelemetryConfiguration
//            {
//                InstrumentationKey = Program.strInstrumentKey
//            };
//            config.TelemetryChannel = new Microsoft.ApplicationInsights.Channel.InMemoryChannel
//            {
//                DeveloperMode = Debugger.IsAttached
//            };
//#if DEBUG
//            config.TelemetryChannel.DeveloperMode = true;
//#endif
            client = new TelemetryClient();
            client.InstrumentationKey = TrxMainClass.strInstrumentKey;
            client.Context.Component.Version = Assembly.GetEntryAssembly().GetName().Version.ToString();
            var newGuid = Guid.NewGuid().ToString();
            client.Context.Session.Id = newGuid;
            client.Context.User.Id = TrxMainClass.strDeviceID + "-" + Assembly.GetEntryAssembly().GetName().Name.ToString();//(Environment.UserName + Environment.MachineName).GetHashCode().ToString();
            client.Context.Device.OperatingSystem = Environment.OSVersion.ToString();
            client.Context.Operation.Id = newGuid;
            client.InstrumentationKey = TrxMainClass.strInstrumentKey;
            return client;
        }

        public static void TrackException(Exception ex)
        {
            var telex = new Microsoft.ApplicationInsights.DataContracts.ExceptionTelemetry(ex)
            {
                SeverityLevel = Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Error
            };
            client.TrackException(telex);
            Flush();
        }

        public static void TrackTrace(string strMsg)
        {
            var telex = new Microsoft.ApplicationInsights.DataContracts.TraceTelemetry(strMsg)
            {
                SeverityLevel = Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Information
            };
            client.TrackTrace(telex);
            Flush();
        }

        public static void TrackTraceWarning(string strMsg)
        {
            var telex = new Microsoft.ApplicationInsights.DataContracts.TraceTelemetry(strMsg)
            {
                SeverityLevel = Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Warning
            };
            client.TrackTrace(telex);
            Flush();
        }

        internal static void Flush()
        {
            client.Flush();
        }

        //something to consider

//        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
//TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
//Application.Current.DispatcherUnhandledException += DispatcherUnhandledException; // WPF app


//private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
//        {
//            var ex = e.ExceptionObject as Exception;
//            Telemetry.TrackException(ex);
//        }

//        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
//        {
//            Telemetry.TrackException(e.Exception);
//        }

//        private static void DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
//        {
//            Telemetry.TrackException(e.Exception);
//        }
    }
}
