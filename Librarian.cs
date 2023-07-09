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
        public bool CheckFoundOfUserByName(string name) {
            return Mapper.First<int>($"SELECT COUNT(*) FROM FAIRA.accounts WHERE name = '{name}' ALLOW FILTERING;") > 0;
        }
        public bool CheckFoundOfUserByEmail(string email) {
            return Mapper.First<int>($"SELECT COUNT(*) FROM FAIRA.accounts WHERE email = '{email}' ALLOW FILTERING;") > 0;
        }
        public bool CheckFoundOfUserByID(string id){
            return Mapper.First<int>($"SELECT COUNT(*) FROM FAIRA.accounts WHERE id='{id}';") > 0;
        }
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
        <summary>В данных пользователя обязательно должны быть: Имя, Пароль.</summary>
        **/
        public UserData? RegisterUser(string ticket) {
            try {
                var res = RegisterUser(Mapper.Single<UserData>($"SELECT id, name, email, password FROM FAIRA.account_creation_requests WHERE ticket = '{ticket}'"));
                Session.Execute($"DELETE FROM FAIRA.account_creation_requests WHERE ticket='{ticket}'");
                return res;
            } catch(System.InvalidOperationException) { return null; }
        }
        /**
        <exception cref="NotFoundException"></exception>
        <exception cref="UnauthorizedAccessException"></exception>
        **/
        public UserData LoginUser(LoginData data) {
            UserData? user = GetUserByName(data.ID);
            if(user is null)
                user = GetUserByEmail(data.ID);
            if(user is null)throw new NotFoundException();
            if(user.Value.Password != data.Password)throw new UnauthorizedAccessException();
            return user.Value;
        }
        public UserData RegisterUser(UserData user) {
            if(String.IsNullOrWhiteSpace(user.Name))throw new ArgumentNullException("Name", "Name is null");
            if(String.IsNullOrWhiteSpace(user.Password))throw new ArgumentNullException("Password", "Password is null");
            if(user.Name.Length > 20)throw new ArgumentOutOfRangeException("Name");
            if(user.Password.Length > 20)throw new ArgumentOutOfRangeException("Password");
            if(CheckFoundOfUserByEmail(user.Email) || CheckFoundOfUserByName(user.Name))
                throw new AlreadyExistsException();
            if(user.ID is null) user.ID = generateUserID();
            Session.Execute($"INSERT INTO FAIRA.accounts (id, characters, email, name, password) VALUES ('{user.ID}', {"{}"}, '{user.Email}', '{user.Name}', '{user.Password}');");
            return user;
        }
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
            if(CheckFoundOfUserByEmail(userData.Email))
                throw new AlreadyExistsException();
            if(Mapper.First<int>($"SELECT COUNT(*) FROM FAIRA.account_creation_requests WHERE email='{userData.Email}' ALLOW FILTERING;") > 0)
                throw new AlreadyExistsException();

            string ticket = (new Random()).NextInt64().ToString();
            while(Session.Execute($"SELECT ticket FROM FAIRA.account_creation_requests WHERE ticket = '{ticket}'").Count() > 0) {
                ticket = (new Random()).NextInt64().ToString();
            }
            Session.Execute($"INSERT INTO FAIRA.account_creation_requests (ticket, email, id, name, password) VALUES ('{ticket}', '{userData.Email}', '{generateUserID()}', '{userData.Name}', '{userData.Password}') USING TTL 86400;");
            return ticket;
        }
        public string GetRequestToRegisterEmail(string email) {
            if(!CheckFoundOfUserByEmail(email))
                throw new NotFoundException();
            var RawData = Session.Execute($"SELECT ticket FROM FAIRA.account_creation_requests WHERE email = '{email}' ALLOW FILTERING;").First();
            return RawData.GetValue<string>("ticket");
        }
        public string CreateRequestToResetPassword(string email) {
            if(!CheckFoundOfUserByEmail(email))
                throw new NotFoundException();
            if(Mapper.First<int>($"SELECT COUNT(*) FROM FAIRA.reset_password_requests WHERE email='{email}' ALLOW FILTERING;") > 0)
                throw new AlreadyExistsException();

            string code = (new Random()).NextInt64().ToString();
            while(Session.Execute($"SELECT code FROM FAIRA.reset_password_requests WHERE code = '{code}'").Count() > 0) {
                code = (new Random()).NextInt64().ToString();
            }
            Session.Execute($"INSERT INTO FAIRA.reset_password_requests (code, email) VALUES ('{code}', '{email}') USING TTL 86400;");
            return code;
        }
        public string GetRequestToResetPassword(string email) {
            if(!CheckFoundOfUserByEmail(email))
                throw new NotFoundException();
            var RawData = Session.Execute($"SELECT code FROM FAIRA.reset_password_requests WHERE email = '{email}' ALLOW FILTERING;").First();
            return RawData.GetValue<string>("code");
        }
        public void SetPassword(string ResetCode, string NewPassword) {
            if(Mapper.First<int>($"SELECT COUNT(*) FROM FAIRA.reset_password_requests WHERE code='{ResetCode}';") < 1)
                throw new NotFoundException();
            string Email = Mapper.First<string>($"SELECT email FROM FAIRA.reset_password_requests WHERE code='{ResetCode}';");
            string ID = Mapper.First<string>($"SELECT id FROM FAIRA.accounts WHERE email='{Email}' ALLOW FILTERING;");
            Session.Execute($"UPDATE FAIRA.accounts SET password = '{NewPassword}' WHERE id = '{ID}';");
            Session.Execute($"DELETE FROM FAIRA.reset_password_requests WHERE code='{ResetCode}';");
        }
    }
}