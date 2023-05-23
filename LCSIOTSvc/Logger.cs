namespace LCS_IoT_Svc
{
    using Serilog;

    public class Logger
    {
        public static void InitLogger()
        {
            Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Information()
                    .WriteTo.Console().WriteTo.File("LcsIoTSvc.log", rollingInterval: RollingInterval.Day, rollOnFileSizeLimit: true)
                    .CreateLogger();
        }

        public static void WrtLogInfo(string strLogMsg)
        {
            Log.Information(strLogMsg);
        }

        public static void WrtLogErr(string strLogMsg)
        {
            Log.Error(strLogMsg);
        }

        public static void WrtLogWarn(string strLogMsg)
        {
            Log.Warning(strLogMsg);
        }
    }
}
