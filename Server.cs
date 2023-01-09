using System.Text;
using System.Net;
using System.Runtime.Caching;

namespace Main {
    sealed class Server
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private static readonly Task ServerWorker = new Task(Server.Worker);
        private static readonly HttpListener listener = new HttpListener();
        private static readonly ObjectCache Cache = MemoryCache.Default;
        private static readonly CacheItemPolicy CachePolicy = new();
        public static readonly string url = Program.ReadSetting("HOST_URL") ?? "http://localhost:1111/";
        public static bool IsServerRunning {get{return _IsServerRunning;} private set{_IsServerRunning=value;}}
        private static volatile bool _IsServerRunning = false;

        public static void Run()
        {
            double minutes;
            try {
                minutes = double.Parse(Program.ReadSetting("CacheTime")??"30");
            } catch(FormatException) {
                Logger.Warn("CacheTime was not in a correct format. Set 30 minutes.");
                minutes = 30;
            }
            CachePolicy.SlidingExpiration = TimeSpan.FromMinutes(minutes);
            Logger.Info("Cache time: {0} minutes.", minutes);
            Logger.Info("Listening for connections on {0}", url);
            listener.Prefixes.Add(url);
            try {
                listener.Start();
                ServerWorker.Start();
                IsServerRunning = true;
            } catch(HttpListenerException e) {
                IsServerRunning = false;
                if(e.ErrorCode == 98) Logger.Error("Address already in use. Change another address.");
            } catch(Exception) {
                IsServerRunning = false;
                throw;
            }
        }
        ///<summary>
        /// Метод принимающий запросы
        ///</summary>
        private static void Worker() {
            Logger.Info("Server is running");

            Task<HttpListenerContext> ctx = listener.GetContextAsync();
            while (IsServerRunning)
            {
                try {
                    // Ждем пока не будет завершено ассинхронное получение контекста.
                    if(ctx.IsCompletedSuccessfully) {
                        // Если контекст успешно получен, отправляем его на обработку в отдельный поток.
                        var result = ctx.GetAwaiter().GetResult();
                        Logger.Info("Request received.");
                        Task.Factory.StartNew(Processing, result, TaskCreationOptions.AttachedToParent);
                    }
                    if(ctx.IsCompleted || ctx.IsCanceled) ctx = listener.GetContextAsync(); // Независимо от результата запускаем следующее ожидание.
                    else Thread.Sleep(10); // Небольшая магическая задержка. Без неё сервер отвечает плохо (Если отвечает в принципе).
                } 
                catch (HttpListenerException err){ Logger.Warn(err, "Error when update the requests"); }
                catch (HttpRequestException err) { Logger.Warn(err, "Error when accepting the request"); }
            }
            return;
        }
        ///<summary>
        /// Метод обрабатывающий запросы
        ///</summary>
        private static async void Processing(object? obj) {
            int threadId = Thread.GetCurrentProcessorId();
            Logger.Info("Request({0}): Processing", threadId);
            HttpListenerContext? ctx = (HttpListenerContext?) obj;
            if(ctx is null) throw new HttpRequestException("Context is null"); 
            HttpListenerRequest  req  = ctx.Request;
            HttpListenerResponse resp = ctx.Response;
            try {
                Logger.Debug("Data in Request ({4})\nMethod: {0}\nURL: {1}\nUserHostName: {2}\nUserAgent: {3}", req.HttpMethod, req.RawUrl, req.UserHostName, req.UserAgent, threadId);
                if(req.RawUrl.EndsWith('/') || req.RawUrl.Contains("../") || string.IsNullOrWhiteSpace(req.RawUrl)) {
                    Logger.Info("Bad request({0}). Abort Responce.", threadId);
                    resp.StatusCode = 400;
                    resp.OutputStream.Close();
                } else {
                    Logger.Info("Request({1}): Request to \"{2}\" HttpMethod: \'{0}\'", req.HttpMethod, threadId, req.RawUrl);
                    switch(req.HttpMethod) {
                        case "TRACE":
                            resp.ContentType = req.ContentType;
                            resp.StatusCode = 200;
                            resp.ContentEncoding = req.ContentEncoding;
                            resp.ContentLength64 = req.ContentLength64;
                            byte[] echodata = Encoding.UTF8.GetBytes("Echo");
                            await resp.OutputStream.WriteAsync(echodata, 0, echodata.Length);
                            break;
                        case "GET":
                            resp.ContentType = "text/html";
                            resp.StatusCode = 200;
                            resp.ContentEncoding = Encoding.UTF8;
                            // Write the response info
                            string? PageData = Cache[req.RawUrl] as string;
                            if (PageData is null) {
                                try {
                                    using (var stream = new StreamReader(Program.HtmlPath + req.RawUrl)) {
                                        PageData = stream.ReadToEnd();
                                        stream.Close();
                                    }
                                    Logger.Info("Caching \'{0}\'", req.RawUrl);
                                    Cache.Set(req.RawUrl, PageData, CachePolicy);
                                } catch (UnauthorizedAccessException e) {
                                    Logger.Error("Access to read \"{0}\" denied", Program.HtmlPath+req.RawUrl);
                                    PageData = "500";
                                    resp.StatusCode = 500;
                                } catch (Exception e) when (e is DirectoryNotFoundException || e is FileNotFoundException) {
                                    Logger.Info("{0} is not found.", req.RawUrl);
                                    PageData = "404";
                                    resp.StatusCode = 404;
                                }
                            }
                            byte[] data = Encoding.UTF8.GetBytes(PageData ?? "500");
                            resp.ContentLength64 = data.LongLength;

                            // Write out to the response stream (asynchronously), then close it
                            await resp.OutputStream.WriteAsync(data, 0, data.Length);
                            break;
                        default:
                            Logger.Info("Bad request({0}). Abort Responce.", threadId);
                            resp.StatusCode = 400;
                            resp.OutputStream.Close();
                            break;
                    }
                }
            } catch (Exception e) {
                Logger.Error(e, "Error with processing request ({0})", threadId);
            } finally {
                resp.Close();
                Logger.Info("Request({0}): Closed", threadId);
            }
        }
        ///<summary>
        // Метод останавливающий сервер.
        ///</summary>
        public static void Stop() {
            Logger.Debug("Request to stop server");
            if(IsServerRunning) {
                IsServerRunning = false;
                listener.Stop();
                listener.Close();
                Logger.Info("Server is stopped");
            }
        }
    }
}