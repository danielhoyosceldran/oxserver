using chatserver.DDBB;
using chatserver.utils;
using System.Text.Json;
using chatserver.authentication;
using System.Security.Policy;
using System.Formats.Asn1;
using Microsoft.Extensions.Options;
using System.Xml.Linq;
using System.Text.Json.Nodes;
using System.ComponentModel;

namespace chatserver.server.APIs
{
    internal class UsersHandler
    {
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
                ExitStatus writeResult = await dDBBHandler.write("users", JsonDocument.Parse(updatedJson).RootElement);

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
            ExitStatus passwordResult = await ddbb.RetrieveField(DataBaseCollections.USERS, UsersDDBBStructure.USERNAME, username, UsersDDBBStructure.PASSWORD);
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

                string storedPassword = await getUserPassword(username!);
                if (storedPassword != password)
                {
                    return new ExitStatus
                    {
                        status = ExitCodes.ERROR,
                        message = "Password is not correct"
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
                ExitStatus result = await dDBBHandler.find(DataBaseCollections.USERS, UsersDDBBStructure.USERNAME, username);

                return new ExitStatus
                {
                    status = result.status,
                    result = result.result,
                    message = result.status == ExitCodes.OK ? "User exist" : "User do not exist",
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

        private async Task<bool> ContactExists(string username, string contact)
        {
            DDBBHandler ddbb = DDBBHandler.Instance;
            ExitStatus searchResult = await ddbb.RetrieveArrayField(
                DataBaseCollections.USERS, 
                UsersDDBBStructure.USERNAME, 
                username, 
                UsersDDBBStructure.CONTACTS, 
                UsersDDBBStructure.USERNAME,
                contact
            );
            return searchResult.status == ExitCodes.OK;
        }

        private async Task<ExitStatus> RetrieveUserName(string username)
        {
            DDBBHandler ddbb = DDBBHandler.Instance;
            ExitStatus result = await ddbb.RetrieveField(DataBaseCollections.USERS, UsersDDBBStructure.USERNAME, username, UsersDDBBStructure.NAME);
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

        

        public async Task<ExitStatus> RetrieveContacts(string username)
        {
            try
            {
                DDBBHandler ddbb = DDBBHandler.Instance;
                ExitStatus result = await ddbb.RetrieveField(DataBaseCollections.USERS, UsersDDBBStructure.USERNAME, username, UsersDDBBStructure.CONTACTS);

                if (result.status != ExitCodes.OK) return result;
                
                string contacts = (string)result.result!;
                JsonObject[] jsonObjectContacts = Utils.ConvertStringToJsonObjectArray(contacts);
                foreach (JsonObject contact in jsonObjectContacts)
                {
                    // 1. Extracting the conversation ID
                    string contactUsername = Utils.GetRequiredJsonProperty(contact, "username");
                    ExitStatus conversationIdResult = await ddbb.RetrieveArrayField(
                        DataBaseCollections.USERS, 
                        UsersDDBBStructure.USERNAME, 
                        username, 
                        UsersDDBBStructure.CONVERSATIONS, 
                        UsersDDBBStructure.CHAT, 
                        contactUsername
                    );

                    if (conversationIdResult.status != ExitCodes.OK) return conversationIdResult;
                    string conversationId = ((Dictionary<string, string>)conversationIdResult.result!)["conversationId"];

                    // 2. Extracting last message
                    ExitStatus lastMessageResult = await ddbb.RetrieveLastArrayElement(
                        DataBaseCollections.CONVERSATIONS, 
                        ConversationDDBBStructure.ID, 
                        conversationId, 
                        ConversationDDBBStructure.MESSAGES
                    );

                    if (lastMessageResult.status == ExitCodes.EXCEPTION)
                    {
                        contact["read"] = true;
                        contact["lastMessage"] = "";
                        continue;
                    }
                    if (lastMessageResult.status != ExitCodes.OK) return lastMessageResult;
                    JsonObject lastMessage = Utils.ConvertIntoJsonObject((string)lastMessageResult.result!);

                    // 3. Extracting needed information
                    string to = Utils.GetRequiredJsonProperty(lastMessage, "to");
                    bool read = Utils.GetRequiredJsonProperty(lastMessage, "read") == "true";
                    read = !(to == username) || read; // If read == false and the message it's for "me" mark as true. Otherwise false.

                    // 4. Saving results
                    contact["read"] = read;
                    contact["lastMessage"] = Utils.GetRequiredJsonProperty(lastMessage, "content"); // TODO: By the moment only works with type "text" messages.
                }

                result.message = result.status == ExitCodes.OK ? "All contacts retrieved successfully!" : "An error has occurred while retrieving the contacts.";
                result.result = JsonSerializer.Serialize(jsonObjectContacts);

                return result;
            }
            catch (Exception ex)
            {
                return new ExitStatus()
                {
                    status = ExitCodes.EXCEPTION,
                    exception = ex.Message
                };
            }
        }

        // Contacts

        private async Task<ExitStatus> AddContactOrGroup(string username, string usernameOrId)
        {
            if (username == usernameOrId) return new ExitStatus
            {
                status = ExitCodes.ERROR,
                message = "You cant add yourselve"
            };

            DDBBHandler ddbb = DDBBHandler.Instance;

            bool isGroup = Utils.IsGroup(usernameOrId);

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

            ExitStatus addResult = await ddbb.AddToArrayField(DataBaseCollections.USERS, UsersDDBBStructure.USERNAME, username,
                isGroup
                ? UsersDDBBStructure.GROUPS
                : UsersDDBBStructure.CONTACTS, newField
            );

            return addResult;
        }

        public async Task<ExitStatus> AddContactHandler(string username, string contactUsername, bool bidirectional = true)
        {
            try
            {
                // TDOD: In future versions we must send a friend request.

                ExitStatus addResult = await AddContactOrGroup(username, contactUsername);

                if (addResult.status == ExitCodes.OK)
                {
                    ExitStatus createConversationResult = await ChatHandler.CreateConversation(username, contactUsername);

                    string conversationId;
                    if (createConversationResult.status == ExitCodes.OK)
                    {
                        conversationId = (string)createConversationResult.result!;
                        ExitStatus addConversationResult = await AddConversationToUser(username, contactUsername, conversationId);
                        if (bidirectional)
                        {
                            // TODO: Instead of "_" we should control the case that this second actions go wrong.
                            // So we should delete the created entites firstly and then return an error to the user.
                            _ = await AddContactOrGroup(contactUsername, username);
                            _ = await AddConversationToUser(contactUsername, username, conversationId);
                        }
                    }

                }

                if (addResult.message == "Contact already added")
                {
                    addResult.status = ExitCodes.OK;
                }

                return addResult;
            }
            catch (Exception ex)
            {
                return new ExitStatus
                {
                    status = ExitCodes.EXCEPTION,
                    exception = ex.Message,
                    message = "Something went wrong adding your contact"
                };
            }
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

        private async Task<ExitStatus> AddConversationToUser(string username, string contactUsername, string conversationId)
        {
            try
            {
                DDBBHandler ddbb = DDBBHandler.Instance;
                var newField = JsonDocument.Parse(
                    JsonSerializer.Serialize(new
                    {
                        chat = contactUsername,
                        conversationId = conversationId,
                    })
                ).RootElement;

                ExitStatus addResult = await ddbb.AddToArrayField(DataBaseCollections.USERS, UsersDDBBStructure.USERNAME, username, UsersDDBBStructure.CONVERSATIONS, newField);

                return addResult;
            }
            catch (Exception ex)
            {
                return new ExitStatus
                {
                    status = ExitCodes.EXCEPTION,
                    exception = ex.Message,
                    message = "Something went wrong adding the conversation"
                };
            }
        }
    }
}
