using WebSocketSharp;
using WebSocketSharp.Server;
using System.Text;

namespace Main {
    public sealed class ProcessingWebsocket : WebSocketBehavior {
        protected override void OnMessage (MessageEventArgs e) {
            Processing(e);
        }
        ///<summary>
        /// Метод обрабатывающий запросы WebSocket
        ///</summary>
        private void Processing(object? obj) {
            var msg = (MessageEventArgs?)obj;
            if(msg is null) return;

            var Logger = NLog.LogManager.GetCurrentClassLogger();
            int threadId = Thread.GetCurrentProcessorId();
            Logger.Info("Websocket({0}): Processing", threadId);

            Logger.Info("Websocket({0}): Data = {1}", threadId, msg.Data);

            Send(msg.Data);
            
            Logger.Info("Websocket({0}): Closed", threadId);
        }
    } 
}