using Microsoft.ServiceBus.Messaging;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsServiceTrxCounter
{
    public static class TrxMainClass
    {
        /// <summary>
        /// The Redis Connection Instatntiate
        /// </summary>
        private static Lazy<ConnectionMultiplexer> LazyConnection;

        private static CancellationTokenSource tokenSource;
        static string ServiceBusConnectionString = string.Empty;
        static string QueueName = string.Empty;
        static string strRedisHost = string.Empty;
        static int intRedisPort = 0;
        static string strRedisKey = string.Empty;
        static string strRedisData = string.Empty;
        public static string strInstrumentKey = string.Empty;
        public static string strDeviceID = string.Empty;
        static QueueClient queueClient;
        static BrokeredMessage Msg;
        //static Serilog.Core.Logger log;
        public static DateTime GetDatetime { get; set; }
        private static Thread thread = new Thread(new ThreadStart(StartTHORApp));

        public static void RunThread()
        {

            try
            {
                thread.Start();
            }
            catch (Exception ex)
            {
                if (thread.IsAlive)
                {
                    thread.Abort();
                }
                Logger.WrtLogErr("Thread Exception - " + ex.Message);
            }
            finally
            {
                if (!thread.IsAlive)
                {
                    //thread.Start();
                    RunThread();
                }
            }
        }

        public static void KillThread()
        {
            try
            {
                if (thread.IsAlive)
                {
                    thread.Abort();
                    Logger.WrtLogWarn("Services Stop");
                }
            }
            catch (Exception ex)
            {
                if (thread.IsAlive)
                {
                    thread.Abort();
                    Logger.WrtLogWarn("Services Stop");
                }
            }
        }

        public static void StartTHORApp()
        {
            //log = new LoggerConfiguration()
            //       .WriteTo.RollingFile(@"C:/LCSR3/THORLog/TrxCounterlog-{Date}.txt", retainedFileCountLimit: 31)
            //       .CreateLogger();
            if (LoadConfiguration())
            {
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
                AppInsightsManager.TrackTrace(strDeviceID + "- Start TrxCounter");
                //log.Information("Start TrxCounter");
                Logger.WrtLogInfo("Start TrxCounter");
                Console.CancelKeyPress += CancelPress;
                MainAsync().GetAwaiter().GetResult();
            }
            else
            {
                AppInsightsManager.TrackTraceWarning(strDeviceID + "- Failed to load Config ");
                //log.Warning("Failed to load Config");
                Logger.WrtLogWarn("Failed to load Config");
            }
            Console.ReadKey();
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            AppInsightsManager.client.TrackException(ex);
            Logger.WrtLogErr(ex.Message);
            //log.Error(ex.Message);
        }

        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            AppInsightsManager.client.TrackException(e.Exception);
            //log.Error(e.Exception.Message);
            Logger.WrtLogErr(e.Exception.Message);
        }

        private static void CancelPress(object sender, ConsoleCancelEventArgs e)
        {
            if (e.Cancel)
            {
                AppInsightsManager.TrackTrace(strDeviceID + "- Console Cancel Event Triggered");
                //log.Warning("Console Cancel Event Triggered");
                Logger.WrtLogWarn("Console Cancel Event Triggered");
                tokenSource.Cancel();
                Thread.Sleep(5000);
            }
        }

        private static ConnectionMultiplexer Connection => LazyConnection.Value;
        private static IDatabase RedisCache => Connection.GetDatabase();

        private static bool LoadConfiguration()
        {
            try
            {
                Logger log = new Logger();
                tokenSource = new CancellationTokenSource();
                var config = new AppSettingsReader();
                ServiceBusConnectionString = StringSec.Decrypt((string)config.GetValue("SBConnString", ServiceBusConnectionString.GetType()));
                QueueName = (string)config.GetValue("SBQueueName", ServiceBusConnectionString.GetType());
                strRedisHost = (string)config.GetValue("RedisHost", strRedisHost.GetType());
                intRedisPort = (int)config.GetValue("RedisPort", intRedisPort.GetType());
                strRedisKey = (string)config.GetValue("RedisKey", strRedisKey.GetType());
                var instrumentKey = (string)config.GetValue("InstrumentKey", strInstrumentKey.GetType());
                if (!string.IsNullOrEmpty(instrumentKey))
                {
                    strInstrumentKey = StringSec.Decrypt(instrumentKey);
                }
                else
                { strInstrumentKey = string.Empty; }
                var pathForConfigFile = (string)config.GetValue("PlazaConfigPath", strRedisKey.GetType());
                var pathForLaneId = (string)config.GetValue("LaneConfigPath", strRedisKey.GetType());
                int intTrack = 0;
                if (File.Exists(pathForConfigFile))
                {
                    var ReadPlzConfig = File.ReadAllLines(pathForConfigFile);

                    foreach (string plzInfo in ReadPlzConfig)
                    {
                        if (!string.IsNullOrEmpty(plzInfo))
                        {
                            intTrack++;
                            if (intTrack == 2)
                            {
                                strDeviceID = plzInfo.Substring(3, 3);
                            }
                        }
                    }
                }
                else
                { Logger.WrtLogWarn("File p_plaza not exist. Please Check path in config"); return false; }
                if (File.Exists(pathForLaneId))
                {
                    var ReadLaneConfig = File.ReadAllLines(pathForLaneId);
                    intTrack = 0;
                    foreach (string laneInfo in ReadLaneConfig)
                    {
                        if (laneInfo.Contains("LaneId"))
                        {
                            if (!string.IsNullOrEmpty(laneInfo))
                            {
                                strDeviceID += laneInfo.Split('=')[1].Substring(1, 2).Trim();
                                break;
                            }
                        }
                    }
                }
                else
                {
                    Logger.WrtLogWarn("File lcs.ini not exist. Please Check path in config"); return false;
                }

                AppInsightsManager.GetAppInsightsClient();
                var configOptions = new ConfigurationOptions();
                configOptions.EndPoints.Add(strRedisHost + ":" + intRedisPort);
                configOptions.ConnectTimeout = 100000;
                configOptions.SyncTimeout = 100000;
                configOptions.AbortOnConnectFail = false;
                configOptions.ConnectRetry = 10;
                LazyConnection = new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(configOptions));
                //AppInsightsManager.TrackTrace("Successfully load Configuration for " + strDeviceID);
                Logger.WrtLogInfo("Successfully load Configuration for " + strDeviceID);
                return true;
            }
            catch (Exception ex)
            {
                //AppInsightsManager.TrackException(ex);
                Logger.WrtLogErr(ex.Message);
                return false;
            }
        }

        static async Task MainAsync()
        {
            AppInsightsManager.TrackTrace(strDeviceID + "- TrxCounter Start pooling Data");
            queueClient = QueueClient.CreateFromConnectionString(ServiceBusConnectionString, QueueName);
            GetDatetime = DateTime.Now;
            while (!tokenSource.IsCancellationRequested)
            {
                if (DateTime.Now.Subtract(GetDatetime).Minutes >= 10)
                {
                    var server = Connection.GetServer(strRedisHost, intRedisPort);
                    foreach (var key in server.Keys())
                    {
                        if (key.ToString().Contains(strRedisKey))
                        {
                            var CurrentSeqHour = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
                            var testMin = Convert.ToInt32(CurrentSeqHour.Substring(14, 2));
                            if (key.ToString().Substring(18, 2) != CurrentSeqHour.Substring(11, 2) && Convert.ToInt32(CurrentSeqHour.Substring(14, 2)) >= 3)
                            {
                                var strData = GetHandshakeData(key);
                                if (!string.IsNullOrWhiteSpace(strData) || strData != string.Empty)
                                {
                                    AppInsightsManager.client.Context.Operation.Id = Guid.NewGuid().ToString();
                                    AppInsightsManager.TrackTrace(strDeviceID + "- Send Handshake Message");
                                    //log.Information("Sending Handshake Msg to IoT - " + strData);
                                    Logger.WrtLogInfo("Sending Handshake Msg to IoT - " + strData);
                                    Console.WriteLine("Handshake Msg - " + strData);
                                    // Send Messages
                                    await SendMessagesAsync(key, strData);
                                    await Task.Delay(TimeSpan.FromSeconds(2), tokenSource.Token);
                                    GetDatetime = DateTime.Now;
                                }
                                else
                                {
                                    Thread.Sleep(TimeSpan.FromMinutes(5));
                                }
                            }
                        }
                    }
                }
                Thread.Sleep(TimeSpan.FromMinutes(5));
            }
        }

        private static string GetHandshakeData(string strKey)
        {
            strRedisData = string.Empty;
            try
            {
                if (RedisCache.IsConnected(strKey))
                {
                    strRedisData = RedisCache.StringGet(strKey, CommandFlags.None);
                }
                return strRedisData;
            }
            catch (Exception ex)
            {
                AppInsightsManager.TrackException(ex);
                Logger.WrtLogErr(ex.Message);
                return strRedisData;
            }
        }

        static async Task SendMessagesAsync(string key, string HandshakeMsg)
        {
            try
            {
                // Create a new message to send to the queue
                using (MemoryStream ms = new MemoryStream())
                {
                    var sw = new StreamWriter(ms, Encoding.UTF8);
                    sw.Write(HandshakeMsg);
                    sw.Flush();
                    ms.Seek(0, SeekOrigin.Begin);
                    Msg = new BrokeredMessage(ms);
                    // Send the message to the queue
                    await queueClient.SendAsync(Msg);
                }

                var CheckTrxCounterKey = RedisCache.StringGet(key);
                while (!CheckTrxCounterKey.IsNullOrEmpty)
                {
                    RedisCache.KeyDelete(key);
                    CheckTrxCounterKey = RedisCache.StringGet(key);
                }
                AppInsightsManager.TrackTrace(strDeviceID + "- Delete Key in Redis - " + key);
                //log.Information("Delete Key in Redis - " + key);
                Logger.WrtLogInfo("Delete Key in Redis - " + key);
            }
            catch (Exception ex)
            {
                AppInsightsManager.TrackException(ex);
                Logger.WrtLogErr(ex.Message);
                //Console.WriteLine($"{DateTime.Now} :: Exception: {exception.Message}");
            }
        }
    }
}
