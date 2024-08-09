using System.Runtime.Caching;

/*
Данный класс отвечает за взаимодействие с сервером посредством консоли прямо во время его работы.
*/
struct Command {
    public Func<string, string[], int> Run;
    public string Description;
}

namespace Main {
    sealed class Terminal {
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        public readonly Dictionary<string, Command> Commands = new();
        private Server server;
        public Terminal(Server server, Librarian librarian) {
            this.server = server;
            var Cache = server.Cache;
            
            Commands.Add("stop", new(){Run=(string origin, string[] args) => { Program.Stop(); return 0; },
             Description="Stops the program."});
            Commands.Add("echo", new(){Run=(string origin, string[] args) => { Console.WriteLine(origin); return 0; },
             Description="Repeats the entered message"});
            Commands.Add("help", new(){
             Description="Shows a list of commands and their descriptions.", Run=(string origin, string[] args) => {
                Console.WriteLine("Commands:"); foreach (string c in Commands.Keys) Console.WriteLine($"  {c} \"{Commands[c].Description}\""); return 0;
            }});
            Commands.Add("readsetting", new(){Run=(string origin, string[] args) => { Logger.Info("Key: {1}; Value: {0}", Program.ReadSetting(Logger, args[1]), args[1]); return 0; },
             Description="Outputs the value of the entered setting key."});
            Commands.Add("readcache", new(){Run=(string origin, string[] args) => { Logger.Info("Key: {1}; Value: {0}", Cache[args[1]] ?? "NULL", args[1]); return 0; },
             Description="Outputs the value of the entered cache key."});
            Commands.Add("cqlsh", new(){
             Description="Runs a command on the data base.", Run=(string origin, string[] args) => {
                Logger.Info("Execute starting.");
                try {
                    var res = librarian.Execute(origin);
                    string columns = "/ ";
                    foreach(var col in res.Columns)
                        columns += col.Name + " / ";
                    Logger.Info(columns);
                    foreach(var row in res) {
                        string data = "/ ";
                        foreach(var v in row)
                            data += (v ?? "null").ToString() + " / ";
                        Logger.Info(data);
                    }
                } catch(Exception e) when (e is Cassandra.InvalidQueryException || e is Cassandra.SyntaxError) {Logger.Info("Error: "+e.Message);}
                Logger.Info("Execute ended.");
                return 0;
            }});
        }
        public void Run() {
            while (Program.IsRunning) {
                try {
                    if(Console.KeyAvailable) {
                        string text = (Console.ReadLine() ?? "").Trim();
                        string[] args = text.Split(" "); // Для удобства сразу разделяем строку на части
                        args[0] = args[0].ToLower();
                        string origin = args.Length > 1 ? text.Substring(args[0].Length+1) : args[0];

                        if (args.Length > 0) {
                            Logger.Debug("Entered: {0}", text);
                            try {
                                int code = Commands[args[0]].Run(origin, args);
                                if(code != 0) Logger.Warn("\'{0}\' exit with code {1}", text, code);
                            } catch(IndexOutOfRangeException) {
                                Logger.Info("Not enough arguments");
                            } catch(KeyNotFoundException) { 
                                Logger.Info("Command {0} is not defined", args[0]);
                                Console.WriteLine("Enter \'help\' to see a list of all commands");
                            }
                        }
                    } else Thread.Sleep(500);
                } catch (InvalidOperationException) {
                } catch (Exception err) {
                    Logger.Warn(err, "Exception during terminal operation");
                }
            }
        }
    }
}