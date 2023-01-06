using System;
using System.IO;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using NLog;

namespace Main
{
    class Program {
        public static readonly string RootPath = Path.GetFullPath(@".");
        public static string LogPath = RootPath+"/Data/Logs/${shortdate}/";
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private static bool ProgramRunning = true;
        public static void Main(string[] Args) {
            var config = new NLog.Config.LoggingConfiguration();

            var logfile    = new NLog.Targets.FileTarget("logfile")       { FileName = LogPath+"Log_${logger}.log",   Layout = "[${longdate}](${level:uppercase=true}) ${message:withexception=true}" };
            var tracefile  = new NLog.Targets.FileTarget("logfile")       { FileName = LogPath+"Trace_${logger}.log", Layout = "[${longdate}](${level:uppercase=true}) ${message:withexception=true}" };
            var logconsole = new NLog.Targets.ConsoleTarget("logconsole") { Layout = "[${date}](${level:uppercase=true})|${logger}| ${message:withexception=true}"};
                        
            // Rules for mapping loggers to targets
            config.AddRule(LogLevel.Info, LogLevel.Fatal, logfile);
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, tracefile);
            config.AddRule(LogLevel.Info,  LogLevel.Fatal, logconsole);
            
            NLog.LogManager.Configuration = config;
            Console.CancelKeyPress += new ConsoleCancelEventHandler(myHandler);

            try {
                Logger.Info("Program is running");
                Server.Run();
                while (Server.RunServer && ProgramRunning) {
                    try {
                        if(Console.KeyAvailable) {
                            string text = (Console.ReadLine() ?? "").ToLower().Trim();
                            if (text.Length > 0) {
                                Logger.Debug($"Entered: {text}");
                                if(text == "stop") break;
                                if(text == "crash") throw new Exception("Crash");
                                if(text == "bag") throw new ArgumentNullException("BAG");
                            }
                        }
                    } catch (ArgumentNullException err) {Logger.Warn(err, "A bag.");}
                }
            } catch (Exception err) {
                Logger.Fatal(err, "Error");
            } finally {
                Server.Stop();
                Logger.Info("Program is stopped");
                NLog.LogManager.Shutdown();
            }
        }
        protected static void myHandler(object sender, ConsoleCancelEventArgs args)
        {
            args.Cancel = true;
            Logger.Info("Ctrl + C is pressed");
            Console.WriteLine("\nAre you sure you want to complete the program? [y/N]");
            if(Char.ToLower(Console.ReadKey().KeyChar) == 'y') {
                Logger.Info("Stopping programm");
                ProgramRunning = false;
            } else Logger.Info("Program continues work");
        }
    }
}