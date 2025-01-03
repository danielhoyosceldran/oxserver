using chatserver.DDBB;
using chatserver.utils;
using System.Text.Json;
using chatserver.authentication;
using System.Security.Policy;
using System.Formats.Asn1;

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

                if ((await UserExists(username!)).status == ExitCodes.OK)
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
                var mutableData = JsonSerializer.Deserialize<Dictionary<string, object>>(root.GetRawText())!;

                mutableData.Add("email", "");
                mutableData.Add("contacts", new List<object>());
                mutableData.Add("groups", new List<object>());
                mutableData.Add("conversations", new List<object>());
                mutableData.Add("lastSeen", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                mutableData.Add("isOnline", true);

                string updatedJson = JsonSerializer.Serialize(mutableData);
                await dDBBHandler.write("users", JsonDocument.Parse(updatedJson).RootElement);

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

        public async Task<string> getUserPassword(string username)
        {
            DDBBHandler ddbb = DDBBHandler.Instance;
            ExitStatus passwordResult = await ddbb.RetrieveField(DB_COLLECTION_NAME, UsersDDBBStructure.USERNAME, username, UsersDDBBStructure.PASSWORD);
            if (passwordResult.status != ExitCodes.OK)
            {
                throw new Exception("Password problems");
            }
            return (string)passwordResult.result!;
        }

        public async Task<ExitStatus> SignInUser(string data)
        {
            try
            {
                JsonDocument parsedData = JsonDocument.Parse(data);
                var root = parsedData.RootElement;

                string? username = root.GetProperty(UsersDDBBStructure.USERNAME).GetString();
                string? password = root.GetProperty(UsersDDBBStructure.PASSWORD).GetString();

                // TODO: CHECK PASSWORD
                string storedPassword = await getUserPassword(username!);
                if (storedPassword != password)
                {
                    return new ExitStatus
                    {
                        status = ExitCodes.ERROR,
                        message = "Password is not correct"
                    };
                }

                Logger.UsersLogger.Debug("[login user] start - " + username);
                Logger.ConsoleLogger.Debug("[UsersAPI - loginUser] - Data rebuda: " + data);

                if ((await UserExists(username!)).status != ExitCodes.OK)
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

        private async Task<ExitStatus> UserExists(string username)
        {
            try
            {
                DDBBHandler dDBBHandler = DDBBHandler.Instance;
                ExitStatus result = await dDBBHandler.find(DB_COLLECTION_NAME, UsersDDBBStructure.USERNAME, username);

                return result;
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

        private async Task<bool> ContactExists(string username, string contact)
        {
            DDBBHandler ddbb = DDBBHandler.Instance;
            return await ddbb.FindInArray(DB_COLLECTION_NAME, UsersDDBBStructure.USERNAME, username, UsersDDBBStructure.CONTACTS, UsersDDBBStructure.USERNAME, contact);
        }

        private async Task<ExitStatus> RetrieveUserName(string username)
        {
            DDBBHandler ddbb = DDBBHandler.Instance;
            ExitStatus result = await ddbb.RetrieveField(DB_COLLECTION_NAME, UsersDDBBStructure.USERNAME, username, UsersDDBBStructure.NAME);
            if (result.status != ExitCodes.OK) result.message = "Contact not found";
            return result;
        }

        private async Task<ExitStatus> RetrieveGroupName(string groupId)
        {
            DDBBHandler ddbb = DDBBHandler.Instance;
            ExitStatus result = await ddbb.RetrieveField(UsersDDBBStructure.GROUPS, "groupId", groupId, UsersDDBBStructure.NAME);
            if (result.status != ExitCodes.OK) result.message = "Group not found";
            return result;
        }

        private bool IsGroup(string groupId)
        {
            return groupId.StartsWith("#gId");
        }

        public async Task<ExitStatus> RetrieveContacts(string username)
        {
            DDBBHandler ddbb = DDBBHandler.Instance;
            ExitStatus result = await ddbb.RetrieveField(DB_COLLECTION_NAME, UsersDDBBStructure.USERNAME, username, UsersDDBBStructure.CONTACTS);
            return result;
        }

        // Contacts
        public async Task<ExitStatus> AddContactOrGroup(string username, string usernameOrId)
        {
            DDBBHandler ddbb = DDBBHandler.Instance;

            bool isGroup = IsGroup(usernameOrId);

            // Check if the user to add exist
            if (!isGroup)
            {
                ExitStatus userExists = await UserExists(usernameOrId);
                if (userExists.status != ExitCodes.OK) return userExists;
            }

            // TODO check if the group exist

            ExitStatus nameResult = isGroup ? await RetrieveGroupName(usernameOrId) : await RetrieveUserName(usernameOrId);
            string name;

            if (nameResult.status == ExitCodes.OK) name = (string)nameResult.result!;
            else return nameResult;

            // TODO: Sh'a de mirar si ja té afegit el group

            if (await ContactExists(username, usernameOrId)) return new ExitStatus()
            {
                status = ExitCodes.ERROR,
                message = "Contact already added",
            };

            // null fields do not appear in the final json
            var options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };


            var newField = JsonDocument.Parse(
                JsonSerializer.Serialize(new
                {
                    name = name,
                    username = isGroup ? null : usernameOrId,
                    groupId = isGroup ? usernameOrId : null
                }, options)
            ).RootElement;

            ExitStatus addResult = await ddbb.AddToArrayField(DB_COLLECTION_NAME, "username", username, 
                isGroup 
                ? UsersDDBBStructure.GROUPS 
                : UsersDDBBStructure.CONTACTS, newField
            );

            return addResult;
        }

        public async Task<ExitStatus> DeleteContactOrGroup(string username, string usernameOrId)
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
