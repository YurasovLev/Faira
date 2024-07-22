using System.Text;
using System.Text.Json;
using System.Net;
using System.Runtime.Caching;
using WebSocketSharp.Server;
using Cassandra;
using MimeKit;
using MailKit.Net.Smtp;

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
        private string MailAddress;
        private string MailPassword;
        private SmtpClient MailClient;
        public Server(int port, ObjectCache cache, Librarian _librain, int updateRequestTimeMS, int checkSpamCountMSGS, double minutes, string mailAddress, string mailPassword) {
            Cache = cache;
            Port = port;
            CheckSpamCountMSGS = updateRequestTimeMS;
            CheckSpamCountMSGS = checkSpamCountMSGS;
            librarian = _librain;
            MailAddress = mailAddress;
            MailPassword = mailPassword;

            MailClient = new();
            // url = int.Parse(Program.ReadSetting(Logger, "PORT") ?? "2020");

            listener = new HttpServer (Port);
            listener.AddWebSocketService<ProcessingWebsocket>("//WebSocket", () => new ProcessingWebsocket(librarian));
            listener.Log.Level = WebSocketSharp.LogLevel.Fatal;

            listener.OnGet += ProcessingGet;
            listener.OnPost += ProcessingPost;

            cacheItemPolicy.SlidingExpiration = TimeSpan.FromMinutes(minutes);
            var SLogger = Logger; // Создано в целях избежания утечек памяти.
            cacheItemPolicy.RemovedCallback += (CacheEntryRemovedArguments args) => {SLogger.Trace($"Cache removed: \'{args.CacheItem.Key}\' by reason \"{args.RemovedReason}\"");};

            SpamPolicy.SlidingExpiration = TimeSpan.FromSeconds(1);
        }
        public async void Run()
        {
            try {
                MailClient.Connect("smtp.gmail.com", 587, false);
                Logger.Info("Mail connected");
                MailClient.Authenticate(MailAddress, MailPassword);
                Logger.Info("Mail authenticated");
                listener.Start();
                if (listener.IsListening) {
                    Logger.Info("Server is running");
                    Logger.Info("Listening on Port {0}, and providing WebSocket services:", listener.Port);
                    foreach(var s in listener.WebSocketServices.Paths)Logger.Info("- {0}", s);
                    IsRunning = true;
                } else throw new Exception("Server don't started");
                while(IsRunning) {
                    _=MailClient.NoOpAsync();
                    await Task.Delay (new TimeSpan (0, 1, 0));
                }
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

            byte[] contents;

            Logger.Debug("Request to get \"{0}\"", req.Url.AbsolutePath);
            switch(req.Url.AbsolutePath) {
                case "/channels": {
                    contents = JsonSerializer.SerializeToUtf8Bytes(librarian.GetChannels());
                    res.StatusCode = (int) HttpStatusCode.OK;
                    res.ContentEncoding = Encoding.UTF8;
                    break;
                }
                default: {
                    Logger.Trace("\"{0}\": Search in html files.", req.Url.AbsolutePath);
                    var path = Program.HtmlPath + req.Url.AbsolutePath;

                    contents = Encoding.UTF8.GetBytes(
                        Program.ReadFileInCache(path, Logger, Cache, cacheItemPolicy) ?? ""
                    );

                    if (contents.Length < 1) {
                        res.StatusCode = (int) HttpStatusCode.NotFound;
                        Logger.Trace("\"{0}\": not found", req.Url.AbsolutePath);
                        break;
                    }
                    Logger.Trace("\"{0}\": found", req.Url.AbsolutePath);

                    if (path.EndsWith (".html")) {
                        Logger.Trace("\"{0}\": is html file", req.Url.AbsolutePath);
                        res.ContentType = "text/html";
                        res.ContentEncoding = Encoding.UTF8;
                    }
                    if (path.EndsWith (".js")) {
                        Logger.Trace("\"{0}\": is javascript file", req.Url.AbsolutePath);
                        res.ContentType = "application/javascript";
                        res.ContentEncoding = Encoding.UTF8;
                    }
                    break;
                }
            }
            res.ContentLength64 = contents.LongLength;
            res.Close(contents, true);
            Logger.Trace("Responce closed.");

        }
        public void ProcessingPost(object? sender, HttpRequestEventArgs e) {
            var req = e.Request;
            if(CheckSpam(e))return;
            Logger.Debug("Request to post \"{0}\"", e.Request.Url.AbsolutePath);
            var res = e.Response;

            byte[] buffer = new byte[req.ContentLength64];
            req.InputStream.Read(buffer, 0, (int)req.ContentLength64);
            string data = Encoding.Default.GetString(buffer);
            Logger.Debug("Data in request: {0}", data);

            switch(e.Request.Url.AbsolutePath) {
                case "/login": {
                    try {
                        UserData user = librarian.LoginUser(JsonSerializer.Deserialize<LoginData>(data));
                        byte[] id = Encoding.UTF8.GetBytes(user.ID);
                        res.ContentLength64 = id.Length;
                        res.OutputStream.Write(id, 0, id.Length);
                        res.StatusCode = (int)HttpStatusCode.Accepted;
                    } catch(NotFoundException) {res.StatusCode = (int)HttpStatusCode.NotFound;
                    } catch(UnauthorizedAccessException) {res.StatusCode = (int)HttpStatusCode.Unauthorized;
                    } catch(Exception err) when (err is ArgumentNullException || err is JsonException) {res.StatusCode = (int)HttpStatusCode.BadRequest;}
                    break;
                }
                case "/user-check-by-name-or-email": {
                    res.StatusCode = librarian.CheckFoundOfUserByName(data) || librarian.CheckFoundOfUserByEmail(data) ?
                        (int)HttpStatusCode.OK : (int)HttpStatusCode.NoContent;
                    break;
                }
                case "/user-check-by-id": {
                    try{
                        UserData user = JsonSerializer.Deserialize<UserData>(data);
                        res.StatusCode = librarian.CheckFoundOfUserByID(user.ID) ? (int)HttpStatusCode.OK : (int)HttpStatusCode.NoContent;
                    } catch(Exception err) when (err is ArgumentNullException || err is JsonException) {res.StatusCode = (int)HttpStatusCode.BadRequest;}
                    break;
                }
                case "/user-register": {
                    UserData userData = JsonSerializer.Deserialize<UserData>(data);
                    Logger.Debug("Register new user: {0} - \'{1}\'", userData.Name, userData.Password);
                    res.StatusCode = (int)HttpStatusCode.Processing;
                    try {
                        string ticket = librarian.CreateRequestToRegisterEmail(userData);
                        SendLetter(userData.Email, "Подтвердите регистрацию", new(MimeKit.Text.TextFormat.Html)
                        {
                            Text = "<p>Это автоматическое сообщение, пожалуйста, не отвечайте на него.</p>"
                            + $"<a href='{req.Url.Scheme}://{req.Url.Host}{(":"+req.Url.Port)}/user-register-from.html?ticket={ticket}'>Подтвердить регистрацию</a>\n"
                        });
                        res.StatusCode = (int)HttpStatusCode.Created;
                    } catch (AlreadyExistsException) {res.StatusCode = (int)HttpStatusCode.Forbidden;
                    } catch (Exception err) when (err is ArgumentNullException || err is JsonException || err is ArgumentOutOfRangeException)
                        {res.StatusCode = (int)HttpStatusCode.BadRequest;}
                    catch(Exception err) {Logger.Error(err, "Error when creating a registration request");res.StatusCode=(int)HttpStatusCode.InternalServerError;}
                    break;
                }
                case "/user-register-confirmation": {
                    try {
                        RegisterDataConfirmation userData = JsonSerializer.Deserialize<RegisterDataConfirmation>(data);
                        Logger.Debug("Confirmation user: {0} - \'{1}\'", userData.Ticket, userData.Email);
                        res.StatusCode = (int)HttpStatusCode.Processing;
                        if(librarian.RegisterUser(userData.Ticket) != null)
                            res.StatusCode = (int)HttpStatusCode.Created;
                        else res.StatusCode = (int)HttpStatusCode.BadRequest;
                    } catch(Exception err) when (err is ArgumentNullException || err is JsonException) {res.StatusCode = (int)HttpStatusCode.BadRequest;}
                    break;
                }
                case "/resend-letter": {
                    string email = data;
                    Logger.Debug("Resend letter for email \'{0}\'", email);
                    res.StatusCode = (int)HttpStatusCode.Processing;
                    try {
                        string ticket = librarian.GetRequestToRegisterEmail(email);
                        SendLetter(email, "Подтвердите регистрацию", new TextPart(MimeKit.Text.TextFormat.Html)
                        {
                            Text = "<p>Это автоматическое сообщение, пожалуйста, не отвечайте на него.</p>"
                            + $"<a href='{req.Url.Scheme}://{req.Url.Host}{(":"+req.Url.Port)}/user-register-from.html?ticket={ticket}'>Подтвердить регистрацию</a>\n"
                        });
                        res.StatusCode = (int)HttpStatusCode.Created;
                    } catch (NotFoundException) {res.StatusCode = (int)HttpStatusCode.NotFound;}
                    catch(Exception err) {Logger.Error(err, "Error when resend letter"); res.StatusCode=(int)HttpStatusCode.InternalServerError;}
                    break;
                }
                case "/send-reset-code": {
                    string email = data;
                    Logger.Debug("Send reset code for email \'{0}\'", email);
                    res.StatusCode = (int)HttpStatusCode.Processing;
                    try {
                        string code = librarian.CreateRequestToResetPassword(email);
                        SendLetter(email, "Изменение пароля", new(MimeKit.Text.TextFormat.Html)
                        {
                            Text = "<p>Это автоматическое сообщение, пожалуйста, не отвечайте на него.</p>"
                            + $"<a href='{req.Url.Scheme}://{req.Url.Host}{(":"+req.Url.Port)}/PasswordRecovery.html?code={code}'>Изменить пароль</a>\n"
                        });
                        res.StatusCode = (int)HttpStatusCode.Created;
                    } catch (AlreadyExistsException)       {
                        string code = librarian.GetRequestToResetPassword(email);
                        SendLetter(email, "Изменение пароля", new(MimeKit.Text.TextFormat.Html)
                        {
                            Text = "<p>Это автоматическое сообщение, пожалуйста, не отвечайте на него.</p>"
                            + $"<a href='{req.Url.Scheme}://{req.Url.Host}{(":"+req.Url.Port)}/PasswordRecovery.html?code={code}'>Изменить пароль</a>\n"
                        });
                        res.StatusCode = (int)HttpStatusCode.Created;
                    } catch (NotFoundException) {res.StatusCode = (int)HttpStatusCode.NotFound;
                    } catch(Exception err) {Logger.Error(err, "Error when send letter"); res.StatusCode=(int)HttpStatusCode.InternalServerError;}
                    break;
                }
                case "/reset-password": {
                    try {
                        LoginData userData = JsonSerializer.Deserialize<LoginData>(data);
                        res.StatusCode = (int)HttpStatusCode.Processing;
                        librarian.SetPassword(userData.ID, userData.Password);
                        res.StatusCode = (int)HttpStatusCode.OK;
                    } catch(NotFoundException) {
                        res.StatusCode = (int)HttpStatusCode.NotFound;
                    } catch(Exception err) when (err is ArgumentNullException || err is JsonException) {res.StatusCode = (int)HttpStatusCode.BadRequest;
                    } catch (Exception err) {
                        Logger.Error(err, "Error when reset password");
                        res.StatusCode = (int)HttpStatusCode.InternalServerError;
                    }
                    break;
                }
                default: {
                    res.StatusCode = (int)HttpStatusCode.NotImplemented;
                    break;
                }
            }
            res.Close();
        }
        private void SendLetter(string Email, string Subject, TextPart Body) {
            using var emailMessage = new MimeMessage() {Subject = Subject, Body = Body};
            emailMessage.From.Add(new MailboxAddress("Администрация Faira", MailAddress));
            emailMessage.To  .Add(new MailboxAddress("", Email));
            MailClient.Send(emailMessage);
            Logger.Debug("Sent letter to the email: {0}", Email);
        }
        public void Stop() {
            var Logger = NLog.LogManager.GetCurrentClassLogger();
            Logger.Debug("Request to stop server");
            if(IsRunning) {
                IsRunning = false;
                MailClient.Disconnect(true);
                MailClient.Dispose();
                listener.Stop();
                Logger.Info("Server is stopped");
            }
        }
        ~Server() {
            Stop();
        }
    }
}