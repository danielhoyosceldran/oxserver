using chatserver.DDBB;
using chatserver.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace chatserver.server.APIs
{
    internal class UsersAPI
    {
        private readonly string DB_COLLECTION_NAME = "users";
        public UsersAPI() { }

        public async Task<ExitStatus> regiterUser(string data)
        {
            try
            {
                JsonDocument parsedData = JsonDocument.Parse(data);
                var root = parsedData.RootElement;

                string? name = root.GetProperty(Users.NAME).GetString();
                string? username = root.GetProperty(Users.USERNAME).GetString();
                string? password = root.GetProperty(Users.PASSWORD).GetString();

                if ((await userExists(username)).status)
                {
                    Logger.ConsoleLogger.Debug("L'usuari ja existeix");
                    return new ExitStatus
                    {
                        code = ExitStatus.Code.ERROR,
                        message = "User already exists."
                    };
                }

                DDBBHandler dDBBHandler = DDBBHandler.getInstance();
                await dDBBHandler.write("users", root);

                Logger.UsersLogger.Debug("[register user] start - " + username);
                Logger.ConsoleLogger.Debug("[UsersAPI - registerUser] - Data rebuda: " + data);

                return new ExitStatus();
            }
            catch (Exception ex) 
            {
                return new ExitStatus
                {
                    code = ExitStatus.Code.EXCEPTION,
                    message = ex.Message,
                    status = false
                };
            }
        }

        private async Task<ExitStatus> userExists(string username)
        {
            try
            {
                DDBBHandler dDBBHandler = DDBBHandler.getInstance();
                utils.ResultJson result = await dDBBHandler.find(DB_COLLECTION_NAME, Users.USERNAME, username);

                return new ExitStatus();
            }
            catch (Exception ex)
            {
                return new ExitStatus
                {
                    code = ExitStatus.Code.EXCEPTION,
                    message = ex.Message,
                    status = false
                };
            }
        }
    }
}
