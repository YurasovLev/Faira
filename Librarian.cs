using Cassandra;
namespace Main {
    public sealed class Librarian {
        private Cluster DataCluster;
        private ISession Session;
        /**
        <summary>
        Класс является посредником между клиентом и базой данных.
        </summary>
        **/
        public Librarian(Cluster dataCluster) {
            DataCluster = dataCluster;
            Session = dataCluster.Connect();
        }
        ///<summary>Проверяет существует-ли пользователь с таким именем.</summary>
        public bool CheckFoundOfUserByName(string name) {
            RowSet result = Session.Execute($"SELECT id FROM FAIRA.accounts WHERE name = '{name}' ALLOW FILTERING;");
            return result.Count() > 0;
        }
        ///<summary>Проверяет существует-ли пользователь с таким ID.</summary>
        public bool CheckFoundOfUserByID(string id){
            var result = Session.Execute($"SELECT id FROM FAIRA.accounts WHERE id = {id};");
            return result.Count() > 0;
        }
        ///<summary>Функция получающая из базы данных данные пользователя.
        public UserData? GetUserByID(string id) {
            if(String.IsNullOrWhiteSpace(id) || id.Contains("null"))return null;
            Row? RawData;
            try {
                RawData = Session.Execute($"SELECT * FROM FAIRA.accounts WHERE id = {id};").First();
            } catch(System.InvalidOperationException) {return null;}
            UserData user = new() {
                ID = RawData.GetValue<System.Numerics.BigInteger>("id").ToString(),
                Email = RawData.GetValue<string>("email"),
                Name = RawData.GetValue<string>("name"),
                Password = RawData.GetValue<string>("password")
            };
            return user;
        }
        public UserData? GetUserByName(string name) {
            if(String.IsNullOrWhiteSpace(name) || name.Contains("null"))return null;
            Row? RawData;
            try {
                RawData = Session.Execute($"SELECT * FROM FAIRA.accounts WHERE name = \'{name}\' ALLOW FILTERING ;").First();
            } catch(System.InvalidOperationException) {return null;}
            UserData user = new() {
                ID = RawData.GetValue<System.Numerics.BigInteger>("id").ToString(),
                Email = RawData.GetValue<string>("email"),
                Name = RawData.GetValue<string>("name"),
                Password = RawData.GetValue<string>("password")
            };
            return user;
        }
        /**
        <summary>Метод для регистрации нового пользователя.
        В данных пользователя обязательно должны быть: Имя, Пароль.</summary>
        <exception cref="ArgumentNullException"></exception>
        <exception cref="MemberAccessException"></exception>
        <exception cref="ArgumentOutOfRangeException"></exception>
        **/
        public UserData RegisterUser(UserData user) {
            if(String.IsNullOrWhiteSpace(user.Name))throw new ArgumentNullException("Name", "Name is null");
            if(String.IsNullOrWhiteSpace(user.Password))throw new ArgumentNullException("Password", "Password is null");
            if(user.Name.Length > 20)throw new ArgumentOutOfRangeException("Name");
            if(user.Password.Length > 20)throw new ArgumentOutOfRangeException("Password");
            if(CheckFoundOfUserByName(user.Name))throw new MemberAccessException("User arleady registered");
            user.ID = generateUserID();
            Session.Execute($"INSERT INTO FAIRA.accounts (id, characters, email, name, password) VALUES ({user.ID}, {"{}"}, '{user.Name+"@email.com"}', '{user.Name}', '{user.Password}');");
            return user;
        }
        ///<summary>Функция для генерации айди пользователя
        private string generateUserID() {
            var time = DateTimeOffset.Now;
            int i = 360;
            while(CheckFoundOfUserByID(Program.ToSnowflake(time)) && i > 0) {
                time.AddSeconds(10);
                i--;
            }
            return Program.ToSnowflake(time);
        }
    }
}