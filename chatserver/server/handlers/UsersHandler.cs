using chatserver.DDBB;
using chatserver.utils;
using System.Text.Json;
using chatserver.authentication;
using System.Security.Policy;

namespace chatserver.server.APIs
{
    internal class UsersHandler
    {
        private readonly string DB_COLLECTION_NAME = "users";
        private UsersHandler() { }
        private static UsersHandler instance = new UsersHandler();
        public static UsersHandler Instance { get { return instance; } }

        public async Task<ExitStatus> signUpUser(string data)
        {
            try
            {
                JsonDocument parsedData = JsonDocument.Parse(data);
                var root = parsedData.RootElement;

                string? name = root.GetProperty(UsersDDBBStructure.NAME).GetString();
                string? username = root.GetProperty(UsersDDBBStructure.USERNAME).GetString();
                string? password = root.GetProperty(UsersDDBBStructure.PASSWORD).GetString();

                Logger.UsersLogger.Debug("[register user] start - " + username);
                Logger.ConsoleLogger.Debug("[UsersAPI - registerUser] - Data rebuda: " + data);

                if ((await userExists(username!)).status == ExitCodes.OK)
                {
                    Logger.UsersLogger.Debug($"[signUpUser] - User {username} already exists");
                    Logger.ConsoleLogger.Debug($"[signUpUser] - User {username} already exists");

                    return new ExitStatus
                    {
                        status = ExitCodes.ERROR,
                        message = "User already exists."
                    };
                }

                DDBBHandler dDBBHandler = DDBBHandler.Instance;
                await dDBBHandler.write("users", root);

                return new ExitStatus()
                {
                    message = "User registered correctly.",
                    result = username,
                };
            }
            catch (Exception ex) 
            {
                ExitStatus ret = new ExitStatus
                {
                    status = ExitCodes.EXCEPTION,
                    exception = ex.Message,
                };
                if (ex.Source == "System.Text.Json")
                {
                    // TODO
                    // Bad request becouse this expetions means that
                    // the content of the recieved json is not correct
                    ret.status = ExitCodes.BAD_REQUEST;
                }
                return ret;
            }
        }

        public async Task<ExitStatus> GetUserId(string username)
        {
            // TODO
            return new ExitStatus()
            {
                result = ""
            };
        }

        public async Task<ExitStatus> signInUser(string data)
        {
            try
            {
                JsonDocument parsedData = JsonDocument.Parse(data);
                var root = parsedData.RootElement;

                string? username = root.GetProperty(UsersDDBBStructure.USERNAME).GetString();
                string? password = root.GetProperty(UsersDDBBStructure.PASSWORD).GetString();


                Logger.UsersLogger.Debug("[login user] start - " + username);
                Logger.ConsoleLogger.Debug("[UsersAPI - loginUser] - Data rebuda: " + data);

                if ((await userExists(username!)).status != ExitCodes.OK)
                {
                    return new ExitStatus
                    {
                        status = ExitCodes.NOT_FOUND,
                        message = "This user does not exist."
                    };
                }

                return new ExitStatus()
                {
                    status = ExitCodes.OK,
                    message = "User singed in succesfuly.",
                    result = username
                };
            }
            catch (Exception ex)
            {
                ExitStatus ret = new ExitStatus
                {
                    status = ExitCodes.EXCEPTION,
                    exception = ex.Message,
                };
                if (ex.Source == "System.Text.Json")
                {
                    // TODO
                    // Bad request becouse this expetions means that
                    // the content of the recieved json is not correct
                    ret.status = ExitCodes.BAD_REQUEST;
                }
                return ret;
            }
        }

        private async Task<ExitStatus> userExists(string username)
        {
            try
            {
                DDBBHandler dDBBHandler = DDBBHandler.Instance;
                ExitStatus result = await dDBBHandler.find(DB_COLLECTION_NAME, UsersDDBBStructure.USERNAME, username);

                return new ExitStatus()
                {
                    status = result.status,
                    message = result.message,
                };
            }
            catch (Exception ex)
            {
                return new ExitStatus
                {
                    status = ExitCodes.EXCEPTION,
                    exception = ex.Message,
                };
            }
        }

        public async Task<ExitStatus> RetrieveContacts(string username)
        {
            DDBBHandler ddbb = DDBBHandler.Instance;
            ExitStatus result = await ddbb.RetrieveField("users", "username", username, "contacts");
            return result;
        }

        // Contacts
        public async Task<ExitStatus> AddContact(string username, string usernameOrId)
        {
            // TODO: Access control for contacts and groups
            return new ExitStatus();
        }

        public async Task<ExitStatus> DeleteContact(string username, string usernameOrId)
        {
            // TODO: Access control for contacts and groups
            return new ExitStatus();
        }

        public async Task<ExitStatus> UpdateContact(string username, string contactUsername, string newName)
        {
            // TODO: Access control for contacts and groups
            return new ExitStatus();
        }
        
        public async Task<ExitStatus> RetrieveMessages(string username, string contactUsername)
        {
            // TODO: Access control for contacts and groups
            return new ExitStatus();
        }
    }
}
