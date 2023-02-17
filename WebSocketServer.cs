using WebSocketSharp;
using WebSocketSharp.Server;
using System.Text;

namespace Main {
    public sealed class ProcessingWebsocket : WebSocketBehavior {
        // private List<WebSocket> sockets = new();
        NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        protected override void OnOpen()
        {
            base.OnOpen();
            Logger.Info("Websocket({0}): Connected", ID);
            Sessions.Broadcast(ID + " --{Connected}--");
        }
        protected override void OnClose(CloseEventArgs e)
        {
            base.OnClose(e);
            Logger.Info("WebSocket({0}): Disconnected", ID);
        }
        protected override void OnError(WebSocketSharp.ErrorEventArgs e)
        {
            Logger.Info(e.ToString());
            Logger.Info(e.Message);
            Logger.Info(e.Exception.ToString());
            Logger.Info(e.Exception.Message);
            Logger.Info(e.Exception.Source);
            Logger.Info(e.Exception.Data.Keys.ToString());
            Logger.Info(e.Exception.Data.Values.ToString());
        }
        protected override void OnMessage (MessageEventArgs msg) {
            if(Program.CheckSpam(ID)) {
                Send("Too many messages");
                Sessions.CloseSession(ID);
                return;
            }
            if(
               msg is null || 
               msg.Data.Contains(ID) || 
               string.IsNullOrWhiteSpace(msg.Data)
            )return;

            Logger.Debug("Websocket({0}): Message \"{1}\"", ID, msg.Data);

            Sessions.Broadcast(ID + ": " + msg.Data);
        }
        ///<summary>
        /// Метод обрабатывающий запросы WebSocket
        ///</summary>
    } 
}