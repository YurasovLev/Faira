using System.Text;
using System.Net;
using System.Runtime.Caching;
using WebSocketSharp.Server;

namespace Main {
    sealed class Server
    {
        private readonly ObjectCache Cache = MemoryCache.Default;
        private readonly CacheItemPolicy cacheItemPolicy;
        public readonly string url = "localhost:1111";
        public readonly string wsurl = "/ws";
        private readonly static string protocol = "http://";
        public readonly int UpdateRequestTimeMS = 10; // Время между проверками появления нового запроса
        public bool IsRunning {get{return _IsRunning;} private set{_IsRunning=value;}}
        private volatile bool _IsRunning = false;
        private HttpListener listener;
        private WebSocketServer WSS; // Web Socket Server
        public Server() {
            var Logger = NLog.LogManager.GetCurrentClassLogger();
            url = Program.ReadSetting(Logger, "HOST_URL") ?? url;
            wsurl = Program.ReadSetting(Logger, "WSS_URL") ?? "/ws";

            WSS = new(wsurl);
            WSS.AddWebSocketService<ProcessingWebsocket>("/");
            // webSocket.OnMessage += (sender, e) => Console.WriteLine(e);

        
            listener = new();
            listener.Prefixes.Add(url);


            UpdateRequestTimeMS = int.Parse(Program.ReadSetting(Logger, "CheckRequestTimeMS") ?? "10");


            double minutes = 30;
            try {
                minutes = double.Parse(Program.ReadSetting(Logger, "CacheTime") ?? "30");
            } catch(FormatException) {
                Logger.Warn("CacheTime was not in a correct format. Set 30 minutes.");
            }
            Logger.Info("Cache time: {0} minutes.", minutes);

            cacheItemPolicy = new();
            cacheItemPolicy.SlidingExpiration = TimeSpan.FromMinutes(minutes);
            var SLogger = Logger; // Создано в целях избежания утечек памяти.
            cacheItemPolicy.RemovedCallback += (CacheEntryRemovedArguments args) => {SLogger.Info($"Cache removed: \'{args.CacheItem.Key}\' by reason \"{args.RemovedReason}\"");};
        }
        public void Run()
        {
            var Logger = NLog.LogManager.GetCurrentClassLogger();
            try {
                WSS.Start();
                Logger.Info("Listening for connections on {0}", wsurl);
                listener.Start();
                Logger.Info("Listening for connections on {0}", url);
            } catch(HttpListenerException e) {
                if(e.ErrorCode == 98) Logger.Error("Address already in use. Change another address.");
                else throw;
                Program.Stop();
                return;
            }
            IsRunning = true;
            Logger.Info("Server is running");
            var ctx = listener.GetContextAsync();
            try {
                while (IsRunning)
                {
                    try {
                        // Ждем пока не будет завершено ассинхронное получение контекста.
                        if(ctx.IsCompletedSuccessfully) {
                            // Если контекст успешно получен, отправляем его на обработку в отдельный поток.
                            var result = ctx.Result;
                            Logger.Info("Request received.");
                            if (!result.Request.IsWebSocketRequest) //Task.Factory.StartNew(Processing, result, TaskCreationOptions.AttachedToParent); 
                                ThreadPool.QueueUserWorkItem(Processing, result);
                            // result.Response.Close();
                            ctx.Dispose();

                            // Processing(result);
                        }
                        if(ctx.IsCompleted || ctx.IsCanceled) ctx = listener.GetContextAsync(); // Независимо от результата запускаем следующее ожидание.
                        else Thread.Sleep(UpdateRequestTimeMS); // Задержка для снижения нагрузки.
                    } 
                    catch (HttpListenerException err) { Logger.Warn(err, "Error when update the requests");   }
                    catch (HttpRequestException err)  { Logger.Warn(err, "Error when accepting the request"); }
                }
            } catch(Exception e) {
                Logger.Fatal(e, "Fatal error during server worked");
                Program.Stop();
            }
        }
        ///<summary>
        /// Метод обрабатывающий обычные запросы
        ///</summary>
        private void Processing(object? obj) {
            var Logger = NLog.LogManager.GetCurrentClassLogger();
            int threadId = Thread.GetCurrentProcessorId();
            Logger.Info("Request({0}): Processing", threadId);
            HttpListenerContext? ctx = (HttpListenerContext?) obj;
            if(ctx is null) throw new HttpRequestException("Context is null");
            HttpListenerRequest  req  = ctx.Request;
            HttpListenerResponse resp = ctx.Response;
            try {
                Logger.Debug("Data in Request ({4})\nMethod: {0}\nURL: {1}\nUserHostName: {2}\nUserAgent: {3}\nHeaders: {5}", req.HttpMethod, req.RawUrl, ctx.Request.RemoteEndPoint.ToString(), req.UserAgent, threadId, string.Join("; ", req.Headers.AllKeys));
                if((req.RawUrl ?? "/").EndsWith('/') || (req.RawUrl ?? "../").Contains("../") || string.IsNullOrWhiteSpace(req.RawUrl)) {
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
                            resp.OutputStream.Write(echodata, 0, echodata.Length);
                            break;
                        case "GET":
                            resp.ContentType = "text/html";
                            resp.StatusCode = 200;
                            resp.ContentEncoding = Encoding.UTF8;
                            var PageData = Program.ReadFileInCache(Program.HtmlPath+req.RawUrl, Logger, Cache, cacheItemPolicy);
                            if (PageData is null) {
                                resp.StatusCode = 404;
                            }
                            // Write the response info
                            
                            byte[] data = Encoding.UTF8.GetBytes(PageData as string ?? "404");
                            resp.ContentLength64 = data.LongLength;

                            // Write out to the response stream (asynchronously), then close it
                            resp.OutputStream.Write(data, 0, data.Length);
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
        public void Stop() {
            var Logger = NLog.LogManager.GetCurrentClassLogger();
            Logger.Debug("Request to stop server");
            if(IsRunning) {
                IsRunning = false;
                WSS.Stop();
                listener.Stop();
                listener.Close();
                Logger.Info("Server is stopped");
            }
        }
        ~Server() {
            Stop();
        }
    }
}