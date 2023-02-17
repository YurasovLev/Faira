using System.Text;
using System.Text.Json;
using System.Net;
using System.Runtime.Caching;
using WebSocketSharp.Server;
using Cassandra;

namespace Main {
    sealed public class UserData {
        public string name {get; set;}
        public string password {get; set;}
    }
    sealed class Server
    {
        public readonly ObjectCache Cache;
        public static readonly CacheItemPolicy SpamPolicy = new();
        public static readonly CacheItemPolicy cacheItemPolicy = new();
        public static int UpdateRequestTimeMS = 10; // Время между проверками появления нового запроса
        public static int CheckSpamCountMSGS = 10; // Сколько сообщений должно быть, что-бы канал считался спамом.
        public readonly int Port; // Порт на котором запускается сервер
        public bool IsRunning {get{return _IsRunning;} private set{_IsRunning=value;}}
        private volatile bool _IsRunning = false;
        private HttpServer listener;
        private NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private ISession DBSession;
        public Server(int port, ObjectCache cache, ISession dbSession, int updateRequestTimeMS, int checkSpamCountMSGS, double minutes) {
            Cache = cache;
            Port = port;
            CheckSpamCountMSGS = updateRequestTimeMS;
            CheckSpamCountMSGS = checkSpamCountMSGS;
            DBSession = dbSession;
            // url = int.Parse(Program.ReadSetting(Logger, "PORT") ?? "2020");

            listener = new HttpServer (Port);
            listener.AddWebSocketService<ProcessingWebsocket>("//WebSocket");
            listener.Log.Level = WebSocketSharp.LogLevel.Fatal;

            listener.OnGet += ProcessingGet;
            listener.OnPost += ProcessingPost;

            cacheItemPolicy.SlidingExpiration = TimeSpan.FromMinutes(minutes);
            var SLogger = Logger; // Создано в целях избежания утечек памяти.
            cacheItemPolicy.RemovedCallback += (CacheEntryRemovedArguments args) => {SLogger.Info($"Cache removed: \'{args.CacheItem.Key}\' by reason \"{args.RemovedReason}\"");};

            SpamPolicy.SlidingExpiration = TimeSpan.FromSeconds(1);
        }
        public void Run()
        {
            try {
                listener.Start();
                if (listener.IsListening) {
                    Logger.Info("Server is running");
                    Logger.Info("Listening on Port {0}, and providing WebSocket services:", listener.Port);
                    foreach(var s in listener.WebSocketServices.Paths)Logger.Info("- {0}", s);
                    IsRunning = true;
                } else throw new Exception("Server don't started");
            } catch(HttpListenerException e) {
                if(e.ErrorCode == 98) Logger.Error("Address already in use. Change another address.");
                else throw;
                Program.Stop();
                return;
            }
        }
        public bool CheckSpam(HttpRequestEventArgs e) {
            var req = e.Request;
            if(Program.CheckSpam(e.Request.RemoteEndPoint.Address.ToString())) {
                Logger.Debug("Spam request");
                e.Response.StatusCode = (int) HttpStatusCode.TooManyRequests;
                return true;
            }
            return false;
        }
        ///<summary>
        /// Метод обрабатывающий запросы
        ///</summary>
        public void ProcessingGet(object? sender, HttpRequestEventArgs e) {
            var req = e.Request;
            if(CheckSpam(e))return;
            if(req.IsWebSocketRequest)return;
            var res = e.Response;

            Logger.Info("Request to get \"{0}\"", e.Request.RawUrl);

            var path = Program.HtmlPath + req.RawUrl;

            byte[] contents = Encoding.UTF8.GetBytes(
                (string?)Program.ReadFileInCache(path, Logger, Cache, cacheItemPolicy) ?? ""
            );

            if (contents.Length < 1) {
                res.StatusCode = (int) HttpStatusCode.NotFound;

                return;
            }

            if (path.EndsWith (".html")) {
                res.ContentType = "text/html";
                res.ContentEncoding = Encoding.UTF8;
            }
            else if (path.EndsWith (".js")) {
                res.ContentType = "application/javascript";
                res.ContentEncoding = Encoding.UTF8;
            }

            res.ContentLength64 = contents.LongLength;

            res.Close (contents, true);
        }
        public void ProcessingPost(object? sender, HttpRequestEventArgs e) {
            var req = e.Request;
            if(CheckSpam(e))return;
            Logger.Info("Request to post \"{0}\"", e.Request.Url.AbsolutePath);
            var res = e.Response;

            byte[] buffer = new byte[req.ContentLength64];
            req.InputStream.Read(buffer, 0, (int)req.ContentLength64);
            string data = Encoding.Default.GetString(buffer);
            Logger.Info(data);

            switch(e.Request.Url.AbsolutePath) {
                case "/user-register":
                    UserData? userData = JsonSerializer.Deserialize<UserData>(data);
                    if(userData is null) break;
                    Logger.Info("User: {0}", userData.name);
                    Logger.Info("Password: {0}", userData.password);
                    var result = DBSession.Execute($"SELECT name FROM FAIRA.accounts WHERE name = '{userData.name}' ALLOW FILTERING;");
                    if(result.Count() > 0) {
                        res.StatusCode = (int)HttpStatusCode.NotAcceptable;
                        break;
                    }
                    res.StatusCode = (int)HttpStatusCode.Accepted;
                    DBSession.Execute($"INSERT INTO FAIRA.accounts (id, characters, email, name, password) VALUES ({DBSession.Execute("SELECT id FROM FAIRA.accounts").Count()+1}, {"{}"}, '{userData.name+"@email.com"}', '{userData.name}', '{userData.password}');");
                    break;
                default:
                    res.StatusCode = (int)HttpStatusCode.NotFound;
                    break;
            }
            res.Close();
        }
        public bool UserRegister(string name, string password) {
            return true;
        }
        ///<summary>
        // Метод останавливающий сервер.
        ///</summary>
        public void Stop() {
            var Logger = NLog.LogManager.GetCurrentClassLogger();
            Logger.Debug("Request to stop server");
            if(IsRunning) {
                IsRunning = false;
                listener.Stop();
                Logger.Info("Server is stopped");
            }
        }
        ~Server() {
            Stop();
        }
    }
}