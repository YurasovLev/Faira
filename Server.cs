using System;
using System.IO;
using System.Text;
using System.Net;
using System.Threading.Tasks;

namespace Main {
    class Server
    {
        private static bool runServer = false;
        public static bool RunServer {get{return runServer;}}
        public static HttpListener listener = new HttpListener();
        public static string url = "http://localhost:8000/";
        public static int requestCount = 0;
        public static string PageData = "<html><p>404</p></html>";
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public static async Task Run()
        {
            runServer = true;
            Logger.Info("Server is running");
            PageData = await (new StreamReader(Program.RootPath+$"/Data/HTML/Base.html")).ReadToEndAsync();
            runServer = true;

            // Создаем считыватель входящих запросов.
            listener.Prefixes.Add(url);
            listener.Start();
            Logger.Info($"Listening for connections on {url}");

            // Запускаем сбор всех входящих запросов
            while (runServer)
            {
                // Ожидаем контекст запроса.
                HttpListenerContext ctx = await listener.GetContextAsync();

                // Получаем Запрос и Ответ. (Request, Responce)
                HttpListenerRequest req = ctx.Request;
                HttpListenerResponse resp = ctx.Response;

                // Отправляем в логи информацию о запросе.
                Logger.Info($"Request #: {++requestCount}");
                Logger.Debug(req.Url.ToString());
                Logger.Debug(req.HttpMethod);
                Logger.Debug(req.UserHostName);
                Logger.Debug(req.UserAgent);

                // Write the response info
                byte[] data = Encoding.UTF8.GetBytes(PageData);
                resp.ContentType = "text/html";
                resp.ContentEncoding = Encoding.UTF8;
                resp.ContentLength64 = data.LongLength;

                // Write out to the response stream (asynchronously), then close it
                await resp.OutputStream.WriteAsync(data, 0, data.Length);
                resp.Close();
            }
        }
        public static void Stop() {
            runServer = false;

            listener.Close();
            Logger.Info("Server is stopped");
        }
    }
}