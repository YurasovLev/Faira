using System.Runtime.Caching;

/*
Данный класс отвечает за взаимодействие с сервером посредством консоли прямо во время его работы.
*/

namespace Main {
    sealed class Terminal {
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        public readonly Dictionary<string, Func<string[], int>> Commands = new Dictionary<string, Func<string[], int>>();
        private Server server;
        public Terminal(Server server) {
            this.server = server;
            var Cache = server.Cache;
            Commands.Add("stop", (string[] args) => { Program.Stop(); return 0; });
            Commands.Add("echo", (string[] args) => { Console.WriteLine(string.Join(" ", args)); return 0; });
            Commands.Add("help", (string[] args) => { Console.WriteLine("Commands:"); foreach (string c in Commands.Keys) Console.WriteLine($"  {c}"); return 0; });
            Commands.Add("readsetting", (string[] args) => { Logger.Info("Key: {1}; Value: {0}", Program.ReadSetting(Logger, args[1]), args[1]); return 0; });
            Commands.Add("readcache", (string[] args) => { Logger.Info("Key: {1}; Value: {0}", Cache[args[1]] ?? "NULL", args[1]); return 0; });
        }
        public void Run() {
            while (Program.IsRunning) {
                try {
                    if(Console.KeyAvailable) {
                        string text = (Console.ReadLine() ?? "").Trim();
                        string[] args = text.Split(" "); // Для удобства сразу разделяем строку на части
                        args[0] = args[0].ToLower();

                        if (args.Length > 0) {
                            Logger.Debug("Entered: {0}", text);
                            try {
                                int code = Commands[args[0]](args);
                                if(code != 0) Logger.Warn("\'{0}\' exit with code {1}", text, code);
                            } catch(IndexOutOfRangeException) {
                                Logger.Info("Not enough arguments");
                            } catch(KeyNotFoundException) { 
                                Logger.Info("Command {0} is not defined", args[0]);
                                Console.WriteLine("Enter \'help\' to see a list of all commands");
                            }
                        }
                    } else Thread.Sleep(500);
                } catch (Exception err) {
                    Logger.Warn(err, "Exception during terminal operation");
                }
            }
        }
    }
}