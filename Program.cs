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
        public static string LogPath = Program.RootPath+"/Data/Logs/file.txt";
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        public static void Main(string[] Args) {
            var config = new NLog.Config.LoggingConfiguration();

            var logfile = new NLog.Targets.FileTarget("logfile") { FileName = path };
                        
            // Rules for mapping loggers to targets            
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logfile);
                        
            // Apply config           
            NLog.LogManager.Configuration = config;
            try
            {
                Logger.Info("Hello world");
                throw new Exception("ERRROROROROROROROROORO!!!!!!!!!!!!!!!!!");
                System.Console.ReadKey();
            }
            catch (Exception ex)
            {
               Logger.Error(ex, "Goodbye cruel world");
            }

            // Logger.Start();
            // try {
            //     Server.Run();
            //     while (Server.RunServer) {
            //         if(Console.KeyAvailable) {
            //             string text = Console.ReadLine() ?? "";
            //             Logger.Log($"Entered: {text}");
            //             if(text.ToLower().Trim() == "exit") break;
            //         }
            //     }
            // } catch (Exception err) {
            //     Console.WriteLine(err);
            //     Logger.Log(err.ToString(), "Error");
            // } finally {
            //     Server.Stop();
            //     Logger.Close();
            // }
        }
    }
}