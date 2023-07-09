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
            Logger.Error($"Error on websocket {ID}");
            Logger.Error("> " + e.ToString());
            Logger.Error("> " + e.Message);
            Logger.Error("> " + e.Exception.ToString());
            Logger.Error("> " + e.Exception.Message);
            Logger.Error("> " + e.Exception.Source);
            Logger.Error("> " + e.Exception.Data.Keys.ToString());
            Logger.Error("> " + e.Exception.Data.Values.ToString());
        }
        private bool Authorize(LoginData data) {
            if(State == WebSocketState.Open) {
                user = librarian.GetUserByID(data.ID);
                if(user is null || user.Value.Password != data.Password) {
                    Send(JsonSerializer.Serialize(new Message() {
                        Type = "Error",
                        AuthorID = "System",
                        Content="User is not found",
                        Code = (int)HttpStatusCode.Unauthorized
                    }));
                    return false;
                }
            }
            return true;
        }
        private void NotLogged() {
            SendError("You are not logged in", (int)HttpStatusCode.Unauthorized);
            Context.WebSocket.Close();
        }
        private void Close(CloseStatusCode code) {Context.WebSocket.Close(code);}
        private void Close() {Context.WebSocket.Close();}
        private void SendError(string Message, int Code)        {Send(JsonSerializer.Serialize(new Message() {Type="Error", AuthorID="System", Content=Message, Code=Code}));}
        private void SendInfo(string Message)                   {Send(JsonSerializer.Serialize(new Message() {Type="Info",  AuthorID="System", Content=Message, Code=(int)HttpStatusCode.OK}));}
        private void SendMessage(string Message, string Author) {Sessions.Broadcast(JsonSerializer.Serialize(new Message() {Type="Text",  AuthorID=Author,   Content=Message, Code=(int)HttpStatusCode.OK}));}
        protected override void OnMessage (MessageEventArgs msg) {
            if(State != WebSocketState.Open)return;
            if(Program.CheckSpam(ID)) {
                SendError("Too many messages!", (int)HttpStatusCode.TooManyRequests);
                Close(CloseStatusCode.ProtocolError);
                return;
            }
            if(msg is null || string.IsNullOrWhiteSpace(msg.Data)) return;
            Logger.Debug("Websocket({0}): Message \'{1}\'", ID, msg.Data);
            try {
                Message message = JsonSerializer.Deserialize<Message>(msg.Data);
                switch(message.Type) {
                    case "Login": {
                        var LoginData = JsonSerializer.Deserialize<LoginData>(message.Content);
                        if(!Authorize(LoginData)) {NotLogged(); break;}
                        Logger.Debug("Websocket({0}): Successfully login <{1} - {2}>", ID, user.Value.Name, user.Value.ID);
                        SendInfo("Login successfully");
                        break;
                    }
                    case "Message": {
                        if(user is null) {NotLogged(); break;}
                        SendMessage(user.Value.Name + ": " + message.Content, user.Value.ID);
                        break;
                    }
                }
            } catch(Exception e)
                when (e is ArgumentNullException || e is JsonException) {
                SendError("Protocol error", (int)HttpStatusCode.BadRequest);
                Close(CloseStatusCode.ProtocolError);
            }
        }
    } 
}