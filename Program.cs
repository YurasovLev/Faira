using System.Configuration;
using System.Runtime.Caching;
using NLog;
using Cassandra;

/*
Это главный класс, который осуществляет запуск и контроль за всеми остальными классами.
Также хранит общие переменные.
*/

namespace Main
{
    sealed class Program {
        public static readonly string RootPath = Path.GetFullPath(@".");
        public static string ConfigsPath = "";
        public static string HtmlPath = "";
        public static bool IsRunning {get{return _IsRunning;} private set{_IsRunning=value;}}
        private static volatile bool _IsRunning = false;
        public static void Main(string[] Args) {
            NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
            ConfigsPath = ReadSetting(Logger, "PathToConfigs") ?? "";
            HtmlPath = ReadSetting(Logger, "PathToHtml") ?? "";



            Console.CancelKeyPress += new ConsoleCancelEventHandler(InterruptConfirmationRequest); // Требование подтверждения прерывания программы.
            NLog.LogManager.Configuration = new NLog.Config.XmlLoggingConfiguration(RootPath + ConfigsPath + "/NLog.config"); // Подгружаем конфиг для NLog

            var port = int.Parse(Program.ReadSetting(Logger, "PORT") ?? "2020");
            var updateRequestTimeMS = int.Parse(Program.ReadSetting(Logger, "CheckRequestTimeMS") ?? "10");
            var checkSpamCountMSGS = int.Parse(Program.ReadSetting(Logger, "CheckSpamCountMSGS") ?? "10");
            double minutes = 30;
            try {
                minutes = double.Parse(Program.ReadSetting(Logger, "CacheTime") ?? "30");
                Logger.Info("Cache time: {0} minutes.", minutes);
            } catch(FormatException) {
                Logger.Warn("CacheTime was not in a correct format. Set 30 minutes.");
            }

            var DBCluster = Cluster.Builder().AddContactPoint("127.0.0.1").Build();
            var DBSession = DBCluster.Connect();
            var server = new Server(port, MemoryCache.Default, DBSession, updateRequestTimeMS, checkSpamCountMSGS, minutes);
            var terminal = new Terminal(server);
            var ServerTask = new Task(server.Run);

            ServerTask.ContinueWith((Task t) => {t.Dispose();});

            try {
                Logger.Info("Program is running");
                ServerTask.Start();
                IsRunning = true;
                terminal.Run();
            } 
            catch (Exception err) {
                Logger.Fatal(err, "Exception when starting program.");
            }
            finally {
                server.Stop();
                DBCluster.Shutdown();
                Logger.Info("Program is stopped.");
                NLog.LogManager.Shutdown();
            }
        }
        public static void Stop() {
            IsRunning = false;
        }
        
        ///<summary>
        /// Если будет нажата комбинация CTRL+C спросить, уверен-ли пользователь в том, что хочет прервать программу.
        ///</summary>
        private static void InterruptConfirmationRequest(object? sender, ConsoleCancelEventArgs args)
        {
            Console.WriteLine("\nAre you sure you want to complete the program? [y/N]");
            if(Char.ToLower(Console.ReadKey().KeyChar) == 'y') {
                Console.WriteLine("Killing program");
            } else {
                Console.WriteLine("Program continues work");
                args.Cancel = true;
            }
        }
        ///<summary>
        /// Читает файл. В случае отсутствия файла вернет null.
        ///</summary>
        public static string? ReadFile(string Path, NLog.Logger Logger) {
            Logger.Info("\"{0}\" reading", Path);
            Path = RootPath + Path;
            string? Data = "";
            try {
                using (var stream = new StreamReader(Path)) {
                    Data = stream.ReadToEnd();
                    stream.Close();
                }
                Logger.Info("\"{0}\" is read", Path);
            } catch (UnauthorizedAccessException) {
                Logger.Error("Access to read \"{0}\" denied", Path);
            } catch (Exception e) when (e is DirectoryNotFoundException || e is FileNotFoundException) {
                Logger.Info("{0} is not found.", Path);
            }
            return Data;
        }
        ///<summary>
        /// Читает файл и кэширует файл. В случае отсутствия файла вернет null.
        ///</summary>
        public static string? ReadFileInCache(string Path, NLog.Logger Logger, ObjectCache Cache, CacheItemPolicy cacheItemPolicy) {
            Logger.Info("\'{0}\' search in cache", Path);
            string? Data = (string?)Cache[Path];
            if (Data is null) {
                Logger.Info("\'{0}\' is missing. Read", Path);
                Data = ReadFile(Path, Logger);
                if (!string.IsNullOrWhiteSpace(Data)) {
                    Logger.Info("\'{0}\' caching", Path);
                    Cache.Set(Path, Data, cacheItemPolicy);
                } else Logger.Info("\'{0}\' is null", Path);
            }
            return Data;
        }
        ///<summary>
        /// Метод для считывания данных из конфига App.config
        ///</summary>
        public static string? ReadSetting(NLog.Logger Logger, string key)  
        {  
            try  
            {
                string? result = ConfigurationManager.AppSettings[key];
                Logger.Info("Read settings with key \'{0}\'", key);
                Logger.Trace("Result: Key \'{0}\', Value \'{1}\'", key, result);
                return result;
            }  
            catch (ConfigurationErrorsException)  
            {  
                Logger.Warn("Error reading app settings. Key: {0}", key);
                return null;
            }
        } 
        public static bool CheckSpam(string ID) {
            var Cache = MemoryCache.Default;
            int? count = (int?)Cache[ID];
            Cache.Set(ID, (count??0)+1, Server.SpamPolicy);
            // catch(NullReferenceException){Cache.Add(ID, 0, Server.SpamPolicy);}
            return count >= Server.CheckSpamCountMSGS;
        }
    }
}