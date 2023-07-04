using Cassandra;
using Cassandra.Mapping;

namespace Main {
    public sealed class Librarian {
        private Cluster DataCluster;
        private ISession Session;
        private Mapper Mapper;
        /**
        <summary>
        Класс является посредником между клиентом и базой данных.
        </summary>
        **/
        public Librarian(Cluster dataCluster) {
            DataCluster = dataCluster;
            Session = dataCluster.Connect();
            Mapper = new(Session);
        }
        ///<summary>Проверяет существует-ли пользователь с таким именем.</summary>
        public bool CheckFoundOfUserByName(string name) {
            return Mapper.First<int>($"SELECT COUNT(*) FROM FAIRA.accounts WHERE name = '{name}' ALLOW FILTERING;") > 0;
        }
        public bool CheckFoundOfUserByEmail(string email) {
            return Mapper.First<int>($"SELECT COUNT(*) FROM FAIRA.accounts WHERE email = '{email}' ALLOW FILTERING;") > 0;
        }
        ///<summary>Проверяет существует-ли пользователь с таким ID.</summary>
        public bool CheckFoundOfUserByID(string id){
            return Mapper.First<int>($"SELECT COUNT(*) FROM FAIRA.accounts WHERE id='{id}';") > 0;
        }
        ///<summary>Функция получающая из базы данных данные пользователя.
        public UserData? GetUserByID(string id) {
            if(String.IsNullOrWhiteSpace(id) || id.Contains("null"))return null;
            try { return Mapper.Single<UserData>($"SELECT id, email, name, password FROM FAIRA.accounts WHERE id = '{id}';");
            } catch(System.InvalidOperationException) {return null;}
        }
        public UserData? GetUserByName(string name) {
            if(String.IsNullOrWhiteSpace(name) || name.Contains("null"))return null;
            try { return Mapper.Single<UserData>($"SELECT id, name, email, password FROM FAIRA.accounts WHERE name = \'{name}\' ALLOW FILTERING ;");
            } catch(System.InvalidOperationException) {return null;}
        }
        public UserData? GetUserByEmail(string email) {
            if(String.IsNullOrWhiteSpace(email) || email.Contains("null"))return null;
            try { return Mapper.Single<UserData>($"SELECT id, name, email, password FROM FAIRA.accounts WHERE email = \'{email}\' ALLOW FILTERING ;");
            } catch(System.InvalidOperationException) {return null;}
        }
        /**
        <summary>Метод для регистрации нового пользователя.
        В данных пользователя обязательно должны быть: Имя, Пароль.</summary>
        <exception cref="ArgumentNullException"></exception>
        <exception cref="MemberAccessException"></exception>
        <exception cref="ArgumentOutOfRangeException"></exception>
        **/
        public UserData? RegisterUser(string ticket) {
            try {
                var res = RegisterUser(Mapper.Single<UserData>($"SELECT id, name, email, password FROM FAIRA.account_creation_requests WHERE ticket = '{ticket}'"));
                Session.Execute($"DELETE FROM FAIRA.account_creation_requests WHERE ticket='{ticket}'");
                return res;
            } catch(System.InvalidOperationException) { return null; }
        }
        public UserData RegisterUser(UserData user) {
            if(String.IsNullOrWhiteSpace(user.Name))throw new ArgumentNullException("Name", "Name is null");
            if(String.IsNullOrWhiteSpace(user.Password))throw new ArgumentNullException("Password", "Password is null");
            if(user.Name.Length > 20)throw new ArgumentOutOfRangeException("Name");
            if(user.Password.Length > 20)throw new ArgumentOutOfRangeException("Password");
            if(CheckFoundOfUserByEmail(user.Email) || CheckFoundOfUserByName(user.Name))
                throw new MemberAccessException("User arleady registered");
            if(user.ID is null) user.ID = generateUserID();
            Session.Execute($"INSERT INTO FAIRA.accounts (id, characters, email, name, password) VALUES ('{user.ID}', {"{}"}, '{user.Email}', '{user.Name}', '{user.Password}');");
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
        ///<summary>Функция создает в базе данных запрос на подтверждение регистрации пользователя.</summary>
        ///<returns>Тикет запроса</returns>
        public string CreateRequestToRegisterEmail(UserData userData) {
            if(CheckFoundOfUserByEmail(userData.Email) || CheckFoundOfUserByName(userData.Name))
                throw new MemberAccessException("User arleady registered");
            if(Mapper.First<int>($"SELECT COUNT(*) FROM FAIRA.account_creation_requests WHERE email='{userData.Email}' ALLOW FILTERING;") > 0) {
                throw new MethodAccessException("Request arleady registered");
            }
            // return result.Count() > 0;
            string ticket = (new Random()).NextInt64().ToString();
            while(Session.Execute($"SELECT ticket FROM FAIRA.account_creation_requests WHERE ticket = '{ticket}'").Count() > 0) {
                ticket = (new Random()).NextInt64().ToString();
            }
            Session.Execute($"INSERT INTO FAIRA.account_creation_requests (ticket, email, id, name, password) VALUES ('{ticket}', '{userData.Email}', '{generateUserID()}', '{userData.Name}', '{userData.Password}') USING TTL 86400;");
            return ticket;
        }
        public string GetRequestToRegisterEmail(string email) {
            if(CheckFoundOfUserByEmail(email))
                throw new MemberAccessException("User arleady registered");
            var RawData = Session.Execute($"SELECT ticket FROM FAIRA.account_creation_requests WHERE email = '{email}' ALLOW FILTERING;").First();
            return RawData.GetValue<string>("ticket");
        }
    }
}