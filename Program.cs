using System.Configuration;
using NLog;

/*
Это главный класс, который осуществляет запуск и контроль за всеми остальными классами.
Также хранит общие переменные.
*/

namespace Main
{
    sealed class Program {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        public static readonly string RootPath = Path.GetFullPath(@".");
        public static readonly string ConfigsPath = RootPath + ReadSetting("PathToConfigs");
        public static readonly string HtmlPath = RootPath + ReadSetting("PathToHtml");
        public static bool IsProgramRunning {get{return _IsProgramRunning;} private set{_IsProgramRunning=value;}}
        private static volatile bool _IsProgramRunning = false;
        public static void Main(string[] Args) {
            Console.CancelKeyPress += new ConsoleCancelEventHandler(InterruptConfirmationRequest); // Требование подтверждения прерывания программы.
            NLog.LogManager.Configuration = new NLog.Config.XmlLoggingConfiguration(ConfigsPath+"/NLog.config"); // Подгружаем конфиг для NLog

            

            try {
                Logger.Info("Program is running");
                Server.Run();
                if(!Server.IsServerRunning) Logger.Fatal("Server did not start.");
                else {
                    IsProgramRunning = true;
                    Terminal.Run();
                }
            } 
            catch (Exception err) {
                Logger.Fatal(err, "Exception when starting program.");
            }
            finally {
                Server.Stop();
                Logger.Info("Program is stopped.");
                NLog.LogManager.Shutdown();
            }
        }
        public static void Stop() {
            IsProgramRunning = false;
        }
        ///<summary>
        /// Если будет нажата комбинация CTRL+C спросить, уверен-ли пользователь в том, что хочет прервать программу.
        ///</summary>
        private static void InterruptConfirmationRequest(object? sender, ConsoleCancelEventArgs args)
        {
            Logger.Info("Ctrl + C is pressed");
            Console.WriteLine("\nAre you sure you want to complete the program? [y/N]");
            if(Char.ToLower(Console.ReadKey().KeyChar) == 'y') {
                Logger.Info("Killing program");
            } else {
                Logger.Info("Program continues work");
                args.Cancel = true;
            }
        }
        ///<summary>
        /// Метод для считывания данных из конфига App.config
        ///</summary>
        public static string? ReadSetting(string key)  
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
    }
}