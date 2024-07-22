namespace Main {
    public struct UserData
    {
        public string ID;
        public string Name {get; set;}
        public string Password {get; set;}
        public string Email {get; set;}
        public UserData(string id, string email, string name, string password) {ID=id;Email=email;Name=name;Password=password;}
    }
    public struct RegisterDataConfirmation
    {
        public string Email {get; set;}
        public string Ticket {get; set;}
    }
    public struct MessageData {
        public DateTimeOffset TimeStamp {get; set;}
        public string ChannelID {get; set;}
        public string Content {get; set;}
        public string AuthorID {get; set;}
        public string Type {get; set;}
        public string ID {get; set;}
        public int Code {get; set;}
    }
    public struct LoginData {
        public string ID {get; set;}
        public string Password {get; set;}
    }
    public class NotFoundException : Exception {}
    public class AlreadyExistsException : Exception {}

    public struct ChannelData {
        public IEnumerable<string> Children {get; set;}
        public string ID {get; set;}
        public string Name {get; set;}
        public string Type {get; set;}
        public ChannelData(string id, string name, string type, IEnumerable<string> children) {ID=id;Name=name;Type=type;Children=children;}
    }
    public class Channel {
        public delegate void MessageHandler(MessageData Message);
        public event MessageHandler? NewMessages;
        public ChannelData Data {get {return new(ID, Name, Type, Children);}}
        public IEnumerable<string> Children {get; set;}
        public string ID {get; set;}
        public string Name {get; set;}
        public string Type {get; set;}
        public Channel(string id, string name, string type, IEnumerable<string> children) {ID=id;Name=name;Type=type;Children=children;}
        public Channel(ChannelData Data) {ID=Data.ID;Name=Data.Name;Type=Data.Type;Children=Data.Children;}
        public void SendMessage(MessageData Message) {
            if(NewMessages is not null) NewMessages(Message);
        }
    }
}