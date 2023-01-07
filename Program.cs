using System;
using System.IO;
using System.Text;
using System.Net;
using System.Threading;
using System.Configuration;
using NLog;

namespace Main
{
    class Program {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        public static readonly string RootPath = Path.GetFullPath(@".");
        public static readonly string ConfigsPath = RootPath + ReadSetting("PathToConfigs");
        public static readonly string HtmlPath = RootPath + ReadSetting("PathToHtml");
        private static bool programRunning = true;
        public static bool ProgramRunning {get{return programRunning;}}
        public static async Task Main(string[] Args) {
            NLog.LogManager.Configuration = new NLog.Config.XmlLoggingConfiguration(ConfigsPath+"/NLog.config");


            Console.CancelKeyPress += new ConsoleCancelEventHandler(myHandler);
            

            try {
                Logger.Info("Program is running");
                Server.Run();
                await Terminal.Run();
            } catch (Exception err) {
                Logger.Fatal(err, "Error");
            } finally {
                Server.Stop();
                Logger.Info("Program is stopped");
                NLog.LogManager.Shutdown();
            }
        }
        public static void Stop() {
            programRunning = false;
        }
        private static void myHandler(object? sender, ConsoleCancelEventArgs args)
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
        public static string? ReadSetting(string key)  
        {  
            try  
            {
                string? result = ConfigurationManager.AppSettings[key] ?? null;
                Logger.Debug("Read settings with key \'{0}\'", key);
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