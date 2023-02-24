using WebSocketSharp;
using WebSocketSharp.Server;
using System.Net;
using System.Text.Json;

namespace Main {
    public sealed class ProcessingWebsocket : WebSocketBehavior {
        // private List<WebSocket> sockets = new();
        NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        public UserData? user {get; private set;}
        private bool start = false;
        private string? TypeOfNextMessage;
        public Librarian librarian;
        public ProcessingWebsocket(Librarian _librain) {librarian = _librain;}
        protected override void OnOpen()
        {
            base.OnOpen();
            Logger.Debug("WebSocket({0}): Connected", ID);
        }
        protected override void OnClose(CloseEventArgs e)
        {
            base.OnClose(e);
            Logger.Debug("WebSocket({0}): Disconnected", ID);
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
                Sessions.CloseSession(ID, (ushort)HttpStatusCode.TooManyRequests, "Too many messages");
                return;
            }
            if(
               msg is null ||
               string.IsNullOrWhiteSpace(msg.Data)
            ) return;
            Logger.Debug("Websocket({0}): Message \'{1}\'", ID, msg.Data);
            if(msg.Data[0] == '>') {
                TypeOfNextMessage = msg.Data;
                return;
            }
            switch(TypeOfNextMessage) {
                case ">Login": {
                    var LoginData = JsonSerializer.Deserialize<LoginData>(msg.Data);
                    user = librarian.GetUserByID(LoginData.ID);
                    if(user is null) {
                        Send(JsonSerializer.Serialize(new Message() {
                            Type = "Error",
                            AuthorID = "System",
                            Content="User is not found"
                        }));
                        Sessions.CloseSession(ID);
                        break;
                    }
                    if(user.Value.Password != LoginData.Password) {
                        Send(JsonSerializer.Serialize(new Message() {
                            Type = "Error",
                            AuthorID = "System",
                            Content="Incorrect password"
                        }));
                        Sessions.CloseSession(ID);
                        break;
                    };
                    Logger.Debug("Websocket({0}): Successfully login <{1} - {2}>", ID, user.Value.Name, user.Value.ID);
                    Send(JsonSerializer.Serialize(new Message() {
                        Type = "Info",
                        AuthorID = "System",
                        Content="Login successfully"
                    }));
                    break;
                }
                case ">Message": {
                    if(user is null) {
                        Send(JsonSerializer.Serialize(new Message() {
                            Type = "Error",
                            AuthorID = "System",
                            Content="You are not logged in"
                        }));
                        Sessions.CloseSession(ID);
                        break;
                    }
                    var message = JsonSerializer.Deserialize<Message>(msg.Data);
                    Sessions.Broadcast(JsonSerializer.Serialize<Message>(
                        new Message(){
                            Type="Text",
                            AuthorID=user.Value.ID,
                            Content=user.Value.Name + ": " + message.Content
                        }
                    ));
                    break;
                }
            }
            TypeOfNextMessage = null;

            // Sessions.Broadcast(ID + ": " + msg.Data);
        }
    } 
}