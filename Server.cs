using System.Text;
using System.Text.Json;
using System.Net;
using System.Runtime.Caching;
using WebSocketSharp.Server;
using Cassandra;

namespace Main {
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
        private Librarian librarian;
        public Server(int port, ObjectCache cache, Librarian _librain, int updateRequestTimeMS, int checkSpamCountMSGS, double minutes) {
            Cache = cache;
            Port = port;
            CheckSpamCountMSGS = updateRequestTimeMS;
            CheckSpamCountMSGS = checkSpamCountMSGS;
            librarian = _librain;
            // url = int.Parse(Program.ReadSetting(Logger, "PORT") ?? "2020");

            listener = new HttpServer (Port);
            listener.AddWebSocketService<ProcessingWebsocket>("//WebSocket", () => new ProcessingWebsocket(librarian));
            listener.Log.Level = WebSocketSharp.LogLevel.Fatal;

            listener.OnGet += ProcessingGet;
            listener.OnPost += ProcessingPost;

            cacheItemPolicy.SlidingExpiration = TimeSpan.FromMinutes(minutes);
            var SLogger = Logger; // Создано в целях избежания утечек памяти.
            cacheItemPolicy.RemovedCallback += (CacheEntryRemovedArguments args) => {SLogger.Debug($"Cache removed: \'{args.CacheItem.Key}\' by reason \"{args.RemovedReason}\"");};

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

            Logger.Info("Request to get \"{0}\"", req.Url.AbsolutePath);
            switch(req.Url.AbsolutePath) {
                default: {
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
                    break;
                }
            }

        }
        public void ProcessingPost(object? sender, HttpRequestEventArgs e) {
            var req = e.Request;
            if(CheckSpam(e))return;
            Logger.Info("Request to post \"{0}\"", e.Request.Url.AbsolutePath);
            var res = e.Response;

            byte[] buffer = new byte[req.ContentLength64];
            req.InputStream.Read(buffer, 0, (int)req.ContentLength64);
            string data = Encoding.Default.GetString(buffer);
            Logger.Debug("Data in request: {0}", data);

            switch(e.Request.Url.AbsolutePath) {
                case "/login": {
                    UserData userData = JsonSerializer.Deserialize<UserData>(data);
                    UserData? user = librarian.GetUserByName(userData.Name);
                    if(user is null || user.Value.Password != userData.Password) {
                        res.StatusCode = (int)HttpStatusCode.NotFound;
                        break;
                    }
                    res.StatusCode = (int)HttpStatusCode.Accepted;
                    byte[] id = Encoding.UTF8.GetBytes(user.Value.ID);
                    res.ContentLength64 = id.Length;
                    res.OutputStream.Write(id, 0, id.Length);
                    break;
                }
                case "/user-check-by-name": {
                    UserData user = JsonSerializer.Deserialize<UserData>(data);
                    // Logger.Info("User: {0}", userDataToCheck.name);
                    // Logger.Info("Password: {0}", userDataToCheck.password);
                    if(librarian.CheckFoundOfUserByName(user.Name))res.StatusCode = (int)HttpStatusCode.OK;
                    else res.StatusCode = (int)HttpStatusCode.NoContent;
                    break;
                }
                case "/user-check-by-id": {
                    UserData user = JsonSerializer.Deserialize<UserData>(data);
                    // Logger.Info("User: {0}", userDataToCheck.name);
                    // Logger.Info("Password: {0}", userDataToCheck.password);
                    if(librarian.CheckFoundOfUserByID(user.ID))res.StatusCode = (int)HttpStatusCode.OK;
                    else res.StatusCode = (int)HttpStatusCode.NoContent;
                    break;
                }
                case "/user-register": {
                    UserData userData = JsonSerializer.Deserialize<UserData>(data);
                    Logger.Debug("Register new user: {0} - \'{1}\'", userData.Name, userData.Password);
                    res.StatusCode = (int)HttpStatusCode.Created;
                    try {
                        userData = librarian.RegisterUser(userData);
                        byte[] id = Encoding.UTF8.GetBytes(userData.ID);
                        res.ContentLength64 = id.Length;
                        res.OutputStream.Write(id, 0, id.Length);
                    } catch (ArgumentNullException) {res.StatusCode = (int)HttpStatusCode.BadRequest;
                    } catch (ArgumentOutOfRangeException) {res.StatusCode = (int)HttpStatusCode.BadRequest;
                    } catch (MemberAccessException) {res.StatusCode = 430;} //Пользователь уже зарегистрирован//
                    break;
                }
                default: {
                    res.StatusCode = (int)HttpStatusCode.NotImplemented;
                    break;
                }
            }
            res.Close();
        }
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