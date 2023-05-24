namespace WindowsServiceTrxCounter
{
    using Serilog;
    using System;

    public class Logger
    {
        public static Serilog.Core.Logger Log { get; set; }
        public Logger()
        {
            Log = new LoggerConfiguration()
                    .ReadFrom.AppSettings()
                    .CreateLogger();

            if (Environment.UserInteractive && System.Diagnostics.Debugger.IsAttached)
            {
                Console.WriteLine("Start Logging");
            }
            else
            {
                Log.Information("Start Logging");
            }
        }

        public static void WrtLogInfo(string strLogMsg)
        {
            if (Environment.UserInteractive && System.Diagnostics.Debugger.IsAttached)
            {
                Console.WriteLine(strLogMsg);
            }
            else
            {
                Log.Information(strLogMsg);
            }
            
        }

        public static void WrtLogErr(string strLogMsg)
        {
            if (Environment.UserInteractive && System.Diagnostics.Debugger.IsAttached)
            {
                Console.WriteLine(strLogMsg);
            }
            else
            {
                Log.Error(strLogMsg);
            }
            
        }

        public static void WrtLogWarn(string strLogMsg)
        {
            if (Environment.UserInteractive && System.Diagnostics.Debugger.IsAttached)
            {
                Console.WriteLine(strLogMsg);
            }
            else
            {
                Log.Warning(strLogMsg);
            }
            
        }
    }
}
