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
    public struct Message {
        public string Content {get; set;}
        public string AuthorID {get; set;}
        public string Type {get; set;}
        public int Code {get; set;}
    }
    public struct LoginData {
        public string ID {get; set;}
        public string Password {get; set;}
    }
    public class NotFoundException : Exception {}
    public class AlreadyExistsException : Exception {}
}