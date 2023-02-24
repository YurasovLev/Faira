namespace Main {
    public struct UserData
    {
        public string ID;
        public string Name {get; set;}
        public string Password {get; set;}
        public string Email {get; set;}
    }
    public struct Message {
        public string Content {get; set;}
        public string AuthorID {get; set;}
        public string Type {get; set;}
    }
    public struct LoginData {
        public string ID {get; set;}
        public string Password {get; set;}
    }
}