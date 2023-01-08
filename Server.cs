using System.Text;
using System.Net;

namespace Main {
    sealed class Server
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private static readonly Task ServerWorker = new Task(Server.Worker);
        private static readonly HttpListener listener = new HttpListener();
        public static readonly string url = Program.ReadSetting("HOST_URL") ?? "http://localhost:1111/";
        public static readonly string PathToHtml = Program.HtmlPath + "/Base.html";
        public static string PageData = (new StreamReader(PathToHtml)).ReadToEnd() ?? "<html><p>404</p></html>";
        public static bool IsServerRunning {get{return _IsServerRunning;} private set{_IsServerRunning=value;}}
        private static volatile bool _IsServerRunning = true;

        public static void Run()
        {
            listener.Prefixes.Add(url);
            listener.Start();

            ServerWorker.Start();
        }
        ///<summary>
        /// Метод принимающий запросы
        ///</summary>
        private static void Worker() {
            Logger.Info("Server is running");
            Logger.Info($"Listening for connections on {url}");

            Task<HttpListenerContext> ctx = listener.GetContextAsync();
            while (IsServerRunning)
            {
                try {
                    // Ждем пока не будет завершено ассинхронное получение контекста.
                    if(ctx.IsCompletedSuccessfully) {
                        // Если контекст успешно получен, отправляем его на обработку в отдельный поток.
                        var result = ctx.GetAwaiter().GetResult();
                        Logger.Info("Request by: ${0}", result.Request.UserHostAddress);
                        Task.Factory.StartNew(Processing, result, TaskCreationOptions.AttachedToParent);
                    }
                    if(ctx.IsCompleted || ctx.IsCanceled) ctx = listener.GetContextAsync(); // Независимо от результата запускаем следующее ожидание.
                    else Thread.Sleep(1); // Небольшая магическая задержка. Без неё сервер отвечает плохо (Если отвечает в принципе).
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
            HttpListenerContext? ctx = (HttpListenerContext?) obj;
            if(ctx is null) throw new HttpRequestException("Context is null"); 
            HttpListenerRequest  req  = ctx.Request;
            HttpListenerResponse resp = ctx.Response;

            Logger.Debug("Data in Request\nMethod: {0}\nURL: {1}\nUserHostName: {2}\nUserAgent: {3}", req.HttpMethod, req.RawUrl, req.UserHostName, req.UserAgent);

            // Write the response info
            byte[] data = Encoding.UTF8.GetBytes(PageData);
            resp.ContentType = "text/html";
            resp.ContentEncoding = Encoding.UTF8;
            resp.ContentLength64 = data.LongLength;

            // Write out to the response stream (asynchronously), then close it
            await resp.OutputStream.WriteAsync(data, 0, data.Length);
            resp.Close();
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