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
            string MailAddress = ReadSetting(Logger, "Mail") ?? "Null";
            string MailPassword = ReadSetting(Logger, "MailPassword") ?? "Null";



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
            var librarian = new Librarian(DBCluster);
            var server = new Server(port, MemoryCache.Default, librarian, updateRequestTimeMS, checkSpamCountMSGS, minutes, MailAddress, MailPassword);
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
            Logger.Debug("\"{0}\" reading", Path);
            Path = RootPath + Path;
            string? Data = "";
            try {
                using (var stream = new StreamReader(Path)) {
                    Data = stream.ReadToEnd();
                    stream.Close();
                }
                Logger.Debug("\"{0}\" is read", Path);
            } catch (UnauthorizedAccessException) {
                Logger.Debug("Access to read \"{0}\" denied", Path);
            } catch (Exception e) when (e is DirectoryNotFoundException || e is FileNotFoundException) {
                Logger.Debug("{0} is not found.", Path);
            }
            return Data;
        }
        ///<summary>
        /// Читает файл и кэширует файл. В случае отсутствия файла вернет null.
        ///</summary>
        public static string? ReadFileInCache(string Path, NLog.Logger Logger, ObjectCache Cache, CacheItemPolicy cacheItemPolicy) {
            Logger.Debug("\'{0}\' search in cache", Path);
            string? Data = (string?)Cache[Path];
            if (Data is null) {
                Logger.Debug("\'{0}\' is missing. Read", Path);
                Data = ReadFile(Path, Logger);
                if (!string.IsNullOrWhiteSpace(Data)) {
                    Logger.Debug("\'{0}\' caching", Path);
                    Cache.Set(Path, Data, cacheItemPolicy);
                } else Logger.Debug("\'{0}\' is null", Path);
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
        /// <summary>
        ///     Resolves the time of which the snowflake is generated.
        /// </summary>
        /// <param name="value">The snowflake identifier to resolve.</param>
        /// <returns>
        ///     A <see cref="DateTimeOffset" /> representing the time for when the object is generated.
        /// </returns>
        public static DateTimeOffset FromSnowflake(string value)
            => DateTimeOffset.FromUnixTimeMilliseconds((long)((ulong.Parse(value) >> 22) + 1420070400000UL));
        /// <summary>
        ///     Generates a pseudo-snowflake identifier with a <see cref="DateTimeOffset"/>.
        /// </summary>
        /// <param name="value">The time to be used in the new snowflake.</param>
        /// <returns>
        ///     A <see cref="UInt64" /> representing the newly generated snowflake identifier.
        /// </returns>
        public static string ToSnowflake(DateTimeOffset value) 
            => (((ulong)value.ToUnixTimeMilliseconds() - 1420070400000UL) << 22).ToString();
    }
}