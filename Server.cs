using System;
using System.IO;
using System.Text;
using System.Net;
using System.Threading.Tasks;

namespace Main {
    class Server
    {
        private static bool runServer = true;
        public static bool RunServer {get{return runServer;}}
        public static HttpListener listener = new HttpListener();
        public static string url = Program.ReadSetting("HOST_URL") ?? "http://localhost:1111/";
        public static string PathToHtml = Program.HtmlPath + "/Base.html";
        public static int requestCount = 0;
        public static string PageData = "<html><p>404</p></html>";
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private static Thread worker = new Thread(Server.Worker);

        public static async void Run()
        {
            listener.Prefixes.Add(url);
            listener.Start();
            PageData = await (new StreamReader(PathToHtml)).ReadToEndAsync();

            worker.Start();
        }
        private static void Worker() {
            runServer = true;
            Logger.Info("Server is running");
            Logger.Info($"Listening for connections on {url}");

            // Запускаем сбор всех входящих запросов
            while (runServer)
            {
                // Ожидаем контекст запроса и отправляем его в ассинхронную функцию.
                try {
                    Processing(listener.GetContext());
                } catch (HttpListenerException) { Logger.Debug("Listener closed"); }
            }
        }
        private static async void Processing(HttpListenerContext ctx) {
            // Получаем Запрос и Ответ. (Request, Responce)
            HttpListenerRequest req = ctx.Request;
            HttpListenerResponse resp = ctx.Response;

            // Отправляем в логи информацию о запросе.
            Logger.Info("Request #: ${0}", ++requestCount);
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
        public static void Stop() {
            Logger.Debug("Request to stop server");
            if(runServer) {
                runServer = false;
                listener.Stop();
                listener.Close();
                Logger.Info("Server is stopped");
            }
        }
    }
}