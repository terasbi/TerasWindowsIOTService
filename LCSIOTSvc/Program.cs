namespace LCS_IoT_Svc
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Newtonsoft.Json;
    using Polly;
    using Serilog;
    using Serilog.Sinks.RollingFile;
    using StackExchange.Redis;
    using System.Linq;
    using System.Timers;

    public static class Program
    {
        /// <summary>
        /// The IOT hub context
        /// </summary>
        private static readonly IoTHubConn IotHubContext = new IoTHubConn();

        private static System.Timers.Timer DataChecker;

        /// <summary>
        /// The Redis Connection Instatntiate
        /// </summary>
        private static Lazy<ConnectionMultiplexer> lazyConnection;

        private static CancellationTokenSource tokenSource;

        private static string strIotServer = string.Empty;
        private static string strPrimaryDeviceKey = string.Empty;
        private static string strSecondaryDeviceKey = string.Empty;
        private static DateTime dtActivate = new DateTime();
        private static int iValidity = 0;
        private static string connMode = string.Empty;
        private static string pidionFilePath = string.Empty;
        private static string pidionArchievePath = string.Empty;
        private static int pidionFileValidity = 0;
        private static string pidionFileKey = string.Empty;
        private static string strKeyMode = string.Empty;
        private static string strPlazaNo = string.Empty;
        private static string strLaneNo = string.Empty;

        private static string strRedisHost = string.Empty;
        private static string strRedisPort = string.Empty;
        private static string strRedisKey = string.Empty;
        private static string strRedisData = string.Empty;
        private static string strData = string.Empty;
        private static Int32 intIntervalHB = 0;
        private static JsonSerializer serializer;
        private static Int32 intRetryCnt = 0;
        private static string[] datFiles;

        public static string StrInstrumentKey = string.Empty;

        public static string StrDeviceId = string.Empty;

        public static Serilog.Core.Logger Log;

        public static Serilog.Core.Logger PidionLog;

        public static string StrIotHubConnString = string.Empty;

        private static DateTime GetTrxDateTime;

        private static ConnectionMultiplexer Connection => lazyConnection.Value;

        private static IDatabase RedisCache => Connection.GetDatabase();

        private static Mutex mutex = null;

        public static string filePath { get; set; } = string.Empty;
        public static string fileName { get; set; } = string.Empty;

        public static void Main(string[] args)
        {
            StartTHORApp();
        }

        public static void StartTHORApp()
        {
            const string appName = "LCS IoT Svc";
            bool createdNew;

            mutex = new Mutex(true, appName, out createdNew);
            if (!createdNew)
            {
                //Log.Warning(appName + " is already running! Exiting the application.");
                Console.WriteLine(appName + " is already running! Exiting the application.");
                Console.ReadKey();
                return;
            }

            try
            {
                Log = new LoggerConfiguration()
                    .WriteTo.RollingFile(@"C:\LCSR3\THORLog\IoTSvclog-{Date}.txt", retainedFileCountLimit: 100)
                    .CreateLogger();
                PidionLog = new LoggerConfiguration()
                    .WriteTo.RollingFile(@"C:\LCSR3\THORLog\PidionThorlog-{Date}.txt", retainedFileCountLimit: 100)
                    .CreateLogger();

                if (LoadConfiguration())
                {
                    Log.Information("LCS Iot Svc Started");
                    Console.WriteLine("After Load Config");
                    AppInsightsManager.GetAppInsightsClient();
                    AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                    TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
                    serializer = new JsonSerializer
                    {
                        NullValueHandling = NullValueHandling.Ignore
                    };

                    tokenSource = new CancellationTokenSource();

                    Console.CancelKeyPress += CancelPress;
                    if (connMode == "1")
                    {
                        Console.WriteLine("Before Data Checker Elapsed");
                        DataChecker = new System.Timers.Timer();
                        DataChecker.Interval = 30000;
                        DataChecker.AutoReset = false;
                        DataChecker.Elapsed += DataChecker_Elapsed;
                        DataChecker.Start();
                        Console.WriteLine("After Data Checker Elapsed");
                    }

                    try
                    {
                        PoolingDataFromRedis(tokenSource.Token).GetAwaiter().GetResult();
                    }
                    catch (UnauthorizedException ioTEx)
                    {
                        AppInsightsManager.TrackException(ioTEx);
                    }
                    catch (Exception ex)
                    {
                        AppInsightsManager.TrackException(ex);
                    }

                    AppInsightsManager.TrackTrace("IoTSvc Start");
                    Log.Information("IoTSvc Start");
                }
                else
                {
                    Log.Warning("IoTSvc Failed to Start. Err Loading Config");
                    Console.WriteLine("Press enter to close...");
                    Console.ReadLine();
                    AppInsightsManager.TrackTraceWarning("IoTSvc Failed to Start. Err Loading Config");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.ReadLine();
            }
        }

        private static void DataChecker_Elapsed(object sender, ElapsedEventArgs e)
        {
            DataChecker.Stop();
            PoolingPidionData().GetAwaiter().GetResult();
            DataChecker.Start();
        }

        ////private static void TaskScheduler_UnobservedTaskException1(object sender, UnobservedTaskExceptionEventArgs e)
        ////{
        ////    Log.Error(e.Exception.Message);
        ////}

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            AppInsightsManager.Client.TrackException(ex);
            Log.Error(ex.Message);
        }

        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            AppInsightsManager.Client.TrackException(e.Exception);
            Log.Error(e.Exception.Message);
        }        

        private static void CancelPress(object sender, ConsoleCancelEventArgs e)
        {
            if (e.Cancel)
            {
                AppInsightsManager.TrackTrace("Console Cancel Event Triggered");
                Log.Warning("Console Cancel Event Triggered");
                tokenSource.Cancel();
                Thread.Sleep(5000);
            }
        }        

        /// <summary>
        /// Load Configuration from AppSetting.Config for 
        /// IoT Setting, Lane Setting and Redis Setting
        /// </summary>
        private static bool LoadConfiguration()
        {
            try
            {
                var config = new System.Configuration.AppSettingsReader();
                StrIotHubConnString = (string)config.GetValue("IoTConnectionString", StrIotHubConnString.GetType());
                StrDeviceId = (string)config.GetValue("DeviceId", StrDeviceId.GetType());
                strIotServer = StrIotHubConnString + ";DeviceId=" + StrDeviceId;
                strPrimaryDeviceKey = StringSec.Decrypt((string)config.GetValue("PrimaryKey", strPrimaryDeviceKey.GetType()));
                strSecondaryDeviceKey = StringSec.Decrypt((string)config.GetValue("SecondaryKey", strSecondaryDeviceKey.GetType()));
                dtActivate = (DateTime)config.GetValue("ActivateDate", dtActivate.GetType());
                iValidity = (int)config.GetValue("Valid", iValidity.GetType()); //// only effect if using SASToken(SASToken only valid as specified)
                connMode = (string)config.GetValue("ConnMode", connMode.GetType()); ////1 = Pidion 0 = Lane
                if (connMode != "0")
                {
                    pidionFileValidity = (int)config.GetValue("PidFileValidity", pidionFileValidity.GetType());
                    pidionFilePath = (string)config.GetValue("PidionFilePath", pidionFilePath.GetType());
                    pidionArchievePath = (string)config.GetValue("PidionArchievePath", pidionArchievePath.GetType());
                    pidionFileKey = (string)config.GetValue("PidionFileKey", pidionFileKey.GetType());
                }                
                strKeyMode = (string)config.GetValue("KeyMode", strKeyMode.GetType());
                strPlazaNo = (string)config.GetValue("PlazaNo", strPlazaNo.GetType());
                strLaneNo = (string)config.GetValue("LaneNo", strLaneNo.GetType());
                strRedisHost = (string)config.GetValue("RedisHost", strRedisHost.GetType());
                strRedisPort = (string)config.GetValue("RedisPort", strRedisPort.GetType());
                strRedisKey = (string)config.GetValue("RedisKey", strRedisKey.GetType());
                intIntervalHB = (int)config.GetValue("IntervalHB", intIntervalHB.GetType());
                var instrumentKey = (string)config.GetValue("InstrumentKey", StrInstrumentKey.GetType());
                
                if (!string.IsNullOrEmpty(instrumentKey))
                {
                    StrInstrumentKey = StringSec.Decrypt(instrumentKey);
                }
                else
                {
                    StrInstrumentKey = string.Empty;
                }

                var configOptions = new ConfigurationOptions();
                configOptions.EndPoints.Add(strRedisHost + ":" + strRedisPort);
                configOptions.ConnectTimeout = 100000;
                configOptions.SyncTimeout = 100000;
                configOptions.AbortOnConnectFail = false;
                configOptions.ConnectRetry = 10;
                lazyConnection = new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(configOptions));
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        private static async Task SendMessage(string strTrxMsg, string strType, DateTime dtLastSentMsg, CancellationToken token)
        {
            try
            {
                string trxDate = string.Empty;
                string trxSeqHour = string.Empty;

                if (strType == "TX")
                {
                    var iotHubMessage = new Messages.IoTHubMessages
                    {
                        Row = strTrxMsg
                    };
                    ////in c# use await. if there is no exception thrown 
                    ////consider as successfull send data to IoT
                    AppInsightsManager.TrackTrace(StrDeviceId + "- Sending Trx Data to IoTHub");
                    Log.Information("Sending Trx Data to IoTHub. Data - " + strTrxMsg);
                    await IotHubContext.SendMessageAsync(iotHubMessage, strType);
                    TrxSeqHourCounter(strTrxMsg, out trxDate, out trxSeqHour);
                    TrxCounterPointer(strTrxMsg, trxDate, trxSeqHour);
                }

                if (DateTime.Now.Subtract(dtLastSentMsg).Minutes >= intIntervalHB)
                {
                    var iotHubHBMessages = new Messages.IoTHubHeartBeatMessages
                    {
                        Status = "OK",
                        PlzNo = strPlazaNo,
                        LaneNo = strLaneNo,
                        DtTimestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                        Version = Assembly.GetExecutingAssembly().GetName().Version.ToString()
                    };

                    ////in c# use await. if there is no exception thrown 
                    ////consider as successfull send data to IoT
                    await IotHubContext.SendMessageAsync(iotHubHBMessages, strType);
                    AppInsightsManager.TrackTrace(StrDeviceId + "- Sending HeartBeat Message to IoTHub");
                    Log.Information("Sending HeartBeat Message to IoTHub");
                    GetTrxDateTime = DateTime.Now;
                }

                intRetryCnt = 0;
            }
            catch (Exception ex)
            {
                AppInsightsManager.TrackException(ex);
                if (strTrxMsg != "OK")
                {
                    await PushUnsuccessfullDatatoRedis(strTrxMsg, token);
                    ////push unsuccessfull data back to redis
                    AppInsightsManager.TrackTraceWarning(StrDeviceId + "- Push Unsuccessfull data to Redis Cache");
                    Log.Error("Push Unsuccessfull data to Redis Cache - " + ex.Message);
                }
                else
                {
                    AppInsightsManager.TrackTraceWarning(StrDeviceId + "- Failed to send HeartBeat message");
                    Log.Error("Failed to send HeartBeat message - " + ex.Message);
                }

                TaskBackoff();
            }
        }

        private static void TaskBackoff()
        {
            int intSleep = 0;
            intRetryCnt++;
            intSleep = 2000 * intRetryCnt;
            if (intSleep >= TimeSpan.FromMinutes(20).TotalMilliseconds)
            {
                intRetryCnt = 0;
                intSleep = 2000;
            }

            Thread.Sleep(intSleep);
        }

        private static void TrxCounterPointer(string strTrxMsg, string trxDate, string trxSeqHour)
        {
            try
            {
                if (trxDate != string.Empty && trxSeqHour != string.Empty)
                {
                    var trxCounterMsg = new Messages.IotHubMessageTrxCounter
                    {
                        PlazaNo = strPlazaNo,
                        LaneNo = strLaneNo
                    };

                    trxCounterMsg.SeqHour = trxSeqHour.PadLeft(2, '0');
                    trxCounterMsg.TransactionDate = trxDate.Substring(0, 4) + "-" + trxDate.Substring(4, 2) + "-" + trxDate.Substring(6, 2);
                    string respRediskey = "rsp/good/" + trxDate + "/" + trxCounterMsg.SeqHour;
                    if (RedisCache.IsConnected(respRediskey, CommandFlags.None))
                    {
                        RedisCounterSetter(true, respRediskey, trxCounterMsg);
                    }
                    else
                    {
                        AppInsightsManager.TrackTraceWarning(StrDeviceId + "- Cant connect to Redis");
                        Log.Warning("Cant connect to Redis");
                    }
                }
                else
                {
                    AppInsightsManager.TrackTraceWarning(StrDeviceId + "- Received string empty either in trxDate or trxSeqHour");
                    Log.Warning("Received string empty either in trxDate or trxSeqHour . Raw Data - " + strTrxMsg);
                }
            }
            catch (Exception ex)
            {
                AppInsightsManager.TrackException(ex);
                Log.Error(ex.Message);
            }
        }

        private static void TrxSeqHourCounter(string rawData, out string trxDate, out string trxSeqHour)
        {
            try
            {
                trxDate = string.Empty;
                trxSeqHour = string.Empty;

                string strDataType = rawData.Substring(0, 2);
                if (strDataType == "06" || strDataType == "08" ||
                    strDataType == "09" || strDataType == "10")
                {
                    trxDate = rawData.Substring(47, 8);
                    trxSeqHour = rawData.Substring(55, 2);
                }
                else if (strDataType == "07" || strDataType == "17" || strDataType == "18")
                {
                    trxDate = rawData.Substring(56, 8);
                    trxSeqHour = rawData.Substring(64, 2);
                }
            }
            catch (Exception ex)
            {
                trxDate = string.Empty;
                trxSeqHour = string.Empty;
                AppInsightsManager.TrackException(ex);
                Log.Error(ex.Message);
            }
        }

        private static bool CheckDataType(string strRawData)
        {
            try
            {
                string strDataType = strRawData.Substring(0, 2);
                if (strDataType == "06" || strDataType == "08" ||
                    strDataType == "09" || strDataType == "10" ||
                    strDataType == "07" || strDataType == "17" ||
                    strDataType == "18")
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                AppInsightsManager.TrackException(ex);
                Log.Error(ex.Message);
                return false;
            }
        }

        private static void RedisCounterSetter(bool flag, RedisKey keys, Messages.IotHubMessageTrxCounter trxCounterMsg)
        {
            try
            {
                string strHandshakeMsg = RedisCache.StringGet(keys.ToString(), CommandFlags.None);
                if (strHandshakeMsg == null)
                {
                    trxCounterMsg.TransactionCount = "1";
                }
                else
                {
                    var objHandshake = JsonConvert.DeserializeObject<Messages.IotHubMessageTrxCounter>(strHandshakeMsg);
                    objHandshake.TransactionCount = (int.Parse(objHandshake.TransactionCount) + 1).ToString();
                    trxCounterMsg = objHandshake;
                }

                string jHandshakeMsg = JsonConvert.SerializeObject(trxCounterMsg, Formatting.Indented);
                AppInsightsManager.TrackTrace(StrDeviceId + "- Set handshake Pointer");
                Log.Information("Set handshake Pointer");
                RedisCache.StringSet(keys.ToString(), jHandshakeMsg, null, When.Always, CommandFlags.None);
            }
            catch (Exception ex)
            {
                AppInsightsManager.TrackException(ex);
            }
        }

        private static string GetRedisData()
        {
            strRedisData = string.Empty;
            try
            {
                if (RedisCache.IsConnected(strRedisKey))
                {
                    strRedisData = RedisCache.ListRightPop(strRedisKey, CommandFlags.None);
                }
                else
                {
                    AppInsightsManager.TrackTraceWarning(StrDeviceId + "- Redis not Connected");
                    Log.Warning("Redis not Connected");
                    Thread.Sleep(3000);
                }

                return strRedisData;
            }
            catch (RedisConnectionException rex)
            {
                AppInsightsManager.TrackException(rex);
                Log.Error(rex.Message);
                return string.Empty;
            }
            catch (Exception ex)
            {
                AppInsightsManager.TrackException(ex);
                Log.Error(ex.Message);
                return string.Empty;
            }
        }

        private static async Task PushUnsuccessfullDatatoRedis(string strUnRedisData, CancellationToken token)
        {
            // Define our policy:
            var policy = Policy.Handle<Exception>().WaitAndRetryForeverAsync(
                sleepDurationProvider: attempt => TimeSpan.FromMinutes(1), // Wait 1 minute between each try.
                onRetry: (exception, calculatedWaitDuration) => // Capture some info for logging!
                {
                    AppInsightsManager.TrackException(exception);
                });

            try
            {
                await policy.ExecuteAsync(async tokenized =>
                {
                    if (RedisCache.IsConnected(strRedisKey, CommandFlags.None))
                    {
                        var retVal = RedisCache.ListRightPush(strRedisKey, strUnRedisData, When.Always, CommandFlags.None);
                        if (retVal == 0)
                        {
                            await PushUnsuccessfullDatatoRedis(strUnRedisData, token);
                        }
                    }
                    else
                    {
                        await PushUnsuccessfullDatatoRedis(strUnRedisData, token);
                        AppInsightsManager.TrackTraceWarning(StrDeviceId + "- Cant connect to Redis! - Push Unsuccessful Data");
                        Log.Warning("Cant connect to Redis! - Push Unsuccessful Data");
                    }
                }, token).ContinueWith(LogExceptions);
            }
            catch (Exception ex)
            {
                await PushUnsuccessfullDatatoRedis(strUnRedisData, token);
                AppInsightsManager.TrackException(ex);
                Log.Error(ex.Message);
            }
        }

        private  static async Task PoolingDataFromRedis(CancellationToken token)
        {
            //// Define policy:
            var policy = Policy.Handle<Exception>().WaitAndRetryForeverAsync(
                sleepDurationProvider: attempt => TimeSpan.FromMinutes(1), //// Wait 1minute between each try.
                onRetry: (exception, calculatedWaitDuration) => //// Capture some info for logging!
                {
                    AppInsightsManager.TrackException(exception);
                });

            if (strKeyMode == "1")
            {
                await IotHubContext.ConnectAsync(strIotServer, StrDeviceId, strPrimaryDeviceKey);
                IoTHubManager.SaveConnMode("1", IotHubContext.IsInitialized);
            }
            else if (strKeyMode == "2")
            {
                await IotHubContext.ConnectAsync(strIotServer, StrDeviceId, strSecondaryDeviceKey);
                IoTHubManager.SaveConnMode("2", IotHubContext.IsInitialized);
            }

            GetTrxDateTime = DateTime.Now;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    strData = string.Empty;
                    AppInsightsManager.Client.Context.Operation.Id = Guid.NewGuid().ToString();
                    await policy.ExecuteAsync(async tokenized =>
                    {
                        strData = GetRedisData();
                        if (!string.IsNullOrWhiteSpace(strData))
                        {
                            //AppInsightsManager.TrackTrace(StrDeviceId + "- Pop Data from RedisCache");
                            //Log.Information("Pop Data from RedisCache - " + strData);
                            Console.WriteLine("Data - " + strData);
                            await SendMessage(strData, "TX", GetTrxDateTime, token);
                        }

                        await SendMessage("OK", "HB", GetTrxDateTime, token);
                        await Task.Delay(TimeSpan.FromSeconds(0.1), token);
                    }, token).ContinueWith(LogExceptions);
                }
                catch (Exception ex)
                {
                    AppInsightsManager.TrackException(ex);
                    Log.Error(ex.Message);
                }
            }
        }

        private static async Task PoolingPidionData()
        {
            try
            {
                InsertPidionData();
                AppInsightsManager.TrackTrace("Insert Pidion Data to Redis");
                PidionLog.Information("Insert Pidion Data to Redis");
            }
            catch (Exception ex)
            {
                AppInsightsManager.TrackException(ex);
                PidionLog.Error(ex.Message);
            }
            finally
            {
                await Task.Delay(TimeSpan.FromSeconds(2), tokenSource.Token);
            }
        }

        private static void LogExceptions(this Task task)
        {
            task.ContinueWith(t =>
            {
                var aggException = t.Exception.Flatten();
                foreach (var exception in aggException.InnerExceptions)
                {
                    AppInsightsManager.TrackException(exception);
                    Log.Error(exception.Message);
                }
            },
            TaskContinuationOptions.OnlyOnFaulted);
        }

        private static bool CheckValidity()
        {
            bool bResp = false;
            TimeSpan dtValidity = DateTime.Now - dtActivate;
            if (dtValidity.Days > iValidity)
            {
                bResp = true;
            }

            return bResp;
        }

        private static void InsertPidionData()
        {
            try
            {
                datFiles = Directory.GetFiles(pidionFilePath, "*.dat")
                                     .Select(Path.GetFileName)
                                     .ToArray();
                Console.WriteLine("Pooling From .Dat File");
                foreach(string datFilesname in datFiles)
                {
                    if(datFilesname.Length == 26)
                    {
                        //if(!CheckFileExist(datFilesname))
                        //{
                            if (CheckFileValidity(datFilesname))
                            {
                                if (RedisCache.IsConnected(pidionFileKey))
                                {
                                    if (!RedisCache.HashExists(pidionFileKey, datFilesname, CommandFlags.None))
                                    {
                                        AppInsightsManager.TrackTrace(StrDeviceId + "- Insert Data to redis for FileName");
                                        PidionLog.Information("Insert Data to redis for FileName - " + datFilesname);
                                        HashEntry[] redisFileHash = { new HashEntry(datFilesname, datFilesname + "|" + DateTime.Now.ToString("ddMMyyyyHHmmss")) };
                                        var lines = File.ReadAllLines(pidionFilePath + @"\" + datFilesname);
                                        foreach (string strPidionRawData in lines)
                                        {
                                            if (CheckDataType(strPidionRawData))
                                            {
                                                RedisCache.ListLeftPush(strRedisKey, strPidionRawData, When.Always, CommandFlags.None);
                                                Console.WriteLine("Pidion Data - " + strPidionRawData);
                                            }
                                        }

                                        RedisCache.HashSet(pidionFileKey, redisFileHash, CommandFlags.None);
                                        Console.WriteLine("Set FileName have been read in RedisCache" + datFilesname);
                                        PidionLog.Information("Set FileName have been read in RedisCache" + datFilesname);
                                    }
                                    else
                                    {
                                        //for (int i = 1; ; ++i)
                                        //{
                                            string name = string.Empty;
                                            try
                                            {
                                                name = Path.Combine(
                                              Path.GetDirectoryName(pidionArchievePath + @"\" + datFilesname),
                                              Path.GetFileNameWithoutExtension(pidionArchievePath + @"\" + datFilesname) +
                                              Path.GetExtension(pidionArchievePath + @"\" + datFilesname));

                                                if (!File.Exists(name))
                                                {
                                                    File.Move(pidionFilePath + @"\" + datFilesname, name);
                                                    break;
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                PidionLog.Error(ex.Message + " - " + name);
                                            }
                                        //}
                                    }
                                }
                                else
                                {
                                    AppInsightsManager.TrackTraceWarning(StrDeviceId + "- Redis not Connected");
                                    PidionLog.Warning("Redis not Connected");
                                    Thread.Sleep(3000);
                                }
                            }
                        //}                        
                    }
                    if (!Directory.Exists(pidionArchievePath))
                    {
                        Directory.CreateDirectory(pidionArchievePath);
                    }
                    if(!File.Exists(pidionArchievePath + @"\" + datFilesname))
                    {
                        File.Move(pidionFilePath + @"\" + datFilesname, pidionArchievePath + @"\" + datFilesname);
                    }
                    else
                    {
                        //for (int i = 1; ; ++i)
                        //{
                            string name = string.Empty;
                            try
                            {
                                name = Path.Combine(
                              Path.GetDirectoryName(pidionArchievePath + @"\" + datFilesname),
                              Path.GetFileNameWithoutExtension(pidionArchievePath + @"\" + datFilesname) +
                              Path.GetExtension(pidionArchievePath + @"\" + datFilesname));

                                if (!File.Exists(name))
                                {
                                    File.Move(pidionFilePath + @"\" + datFilesname, name);

                                    break;
                                }
                            }
                            catch(Exception ex)
                            {
                                PidionLog.Error(ex.Message + " - " + name);
                            }                            
                        //}
                    }
                    
                    Thread.Sleep(300);                   
                }                
            }
            catch (Exception ex)
            {
                AppInsightsManager.TrackException(ex);
                PidionLog.Error(ex.Message);
            }
        }

        private static bool CheckFileValidity(string FileName)
        {
            try
            {
                int Year = Convert.ToInt32(FileName.Substring(3, 4));
                int Month = Convert.ToInt32(FileName.Substring(7, 2));
                int Day = Convert.ToInt32(FileName.Substring(9, 2));
                DateTime IntervalDays = new DateTime();
                IntervalDays = DateTime.Today.AddDays(-pidionFileValidity);

                DateTime FileDate = new DateTime(Year, Month, Day);
                if(FileDate >= IntervalDays && IntervalDays<=DateTime.Today)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch(Exception ex)
            {
                PidionLog.Error(ex.Message);
                return false;
            }
        }

        private static bool CheckFileExist(string FileName)
        {
            bool ret = false;
            try
            {
                string[] files = Directory.GetFiles(filePath);

                foreach (string file in files)
                {
                    var fileExt = Path.GetExtension(file).ToUpper();
                    if (fileExt.Contains(fileName.ToUpper()))
                    {
                        ret = true;
                    }
                }

                return ret;
            }
            catch(Exception ex)
            {
                PidionLog.Error(ex.Message);
                return false;
            }
        }
    }
}
