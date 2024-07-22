using WebSocketSharp;
using WebSocketSharp.Server;
using System.Net;
using System.Text.Json;
using System.Diagnostics.CodeAnalysis;

namespace Main {
    public sealed class ProcessingWebsocket : WebSocketBehavior {
        // private List<WebSocket> sockets = new();
        NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        public UserData? user {get; private set;}
        private bool start = false;
        public Librarian librarian;
        public Channel? channel;
        public ProcessingWebsocket(Librarian _librain) {librarian = _librain;}
        protected override void OnOpen()
        {
            base.OnOpen();
            Logger.Debug("WebSocket({0}): Connected", ID);
        }
        protected override void OnClose(CloseEventArgs e)
        {
            base.OnClose(e);
            if(channel is not null) channel.NewMessages -= ProcessMessages;
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
                    Send(JsonSerializer.Serialize(new MessageData() {
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
        private void SendError(string Message, int Code)        {Send(JsonSerializer.Serialize(new MessageData() {Type="Error", AuthorID="System", Content=Message, Code=Code, TimeStamp=DateTimeOffset.Now}));}
        private void SendInfo(string Message)                   {Send(JsonSerializer.Serialize(new MessageData() {Type="Info",  AuthorID="System", Content=Message, Code=(int)HttpStatusCode.OK, TimeStamp=DateTimeOffset.Now}));}
        private void SendMessage(string Message, string Author) {
            // Sessions.Broadcast(JsonSerializer.Serialize(new MessageData() {
            //     Type="Text",  AuthorID=Author,   Content=Message, Code=(int)HttpStatusCode.OK, TimeStamp=DateTimeOffset.Now
            // }));
            if(channel is not null)
                channel.SendMessage(new MessageData() {
                    Type="Text",  AuthorID=Author,   Content=Message, Code=(int)HttpStatusCode.OK, TimeStamp=DateTimeOffset.Now
                });
            else throw new NullReferenceException("No channel selected");
        }
        protected override void OnMessage (MessageEventArgs msg) {
            if(State != WebSocketState.Open)return;
            if(Program.CheckSpam(ID)) {
                SendError("Too many messages!", (int)HttpStatusCode.TooManyRequests);
                return;
            }
            if(msg is null || string.IsNullOrWhiteSpace(msg.Data)) return;
            Logger.Debug("Websocket({0}): Message \'{1}\'", ID, msg.Data);
            try {
                MessageData message = JsonSerializer.Deserialize<MessageData>(msg.Data);
                switch(message.Type) {
                    case "Login": {
                        var LoginData = JsonSerializer.Deserialize<LoginData>(message.Content);
                        user = librarian.GetUserByID(LoginData.ID);
                        if(user is null || user.Value.Password != LoginData.Password) {
                            Send(JsonSerializer.Serialize(new MessageData() {
                                Type = "Error",
                                AuthorID = "System",
                                Content="User is not found",
                                Code = (int)HttpStatusCode.Unauthorized
                            }));
                            NotLogged();
                            break;
                        }
                        channel = librarian.GetChannelByID(message.ChannelID);
                        if(channel is not null) channel.NewMessages += ProcessMessages;
                        Logger.Debug("Websocket({0}): Successfully login <{1} - {2}>", ID, user.Value.Name, user.Value.ID);
                        SendInfo("Login successfully");
                        break;
                    }
                    case "Message": {
                        if(user is null) {NotLogged(); break;}
                        if(message.Content.Length > 200) {
                            SendError("Too long", (int)HttpStatusCode.RequestEntityTooLarge);
                            return;
                        }
                        try {SendMessage(user.Value.Name + ": " + message.Content, user.Value.ID);}
                        catch(NullReferenceException) {SendError("No channel selected", (int)HttpStatusCode.BadRequest);}
                        break;
                    }
                    case "ChangeChannel": {
                        if(user is null) {NotLogged(); break;}
                        if(message.Content.Length > 200) {
                            SendError("Too long", (int)HttpStatusCode.RequestEntityTooLarge);
                            return;
                        }
                        if(channel is not null) channel.NewMessages -= ProcessMessages;
                        channel = librarian.GetChannelByID(message.Content);
                        if(channel is null)
                            SendError("Channel not found", (int)HttpStatusCode.NotFound);
                        else channel.NewMessages += ProcessMessages;
                        break;
                    }
                }
            } catch(Exception e)
                when (e is ArgumentNullException || e is JsonException) {
                SendError("Protocol error", (int)HttpStatusCode.BadRequest);
                Close(CloseStatusCode.ProtocolError);
            }
        }
        public void ProcessMessages(MessageData Message) {
            if(user is not null)
                Send(JsonSerializer.Serialize(Message));
        }
    } 
}