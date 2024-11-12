using chatserver.DDBB;
using chatserver.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace chatserver.server.APIs
{
    internal class UsersHandler
    {
        private readonly string DB_COLLECTION_NAME = "users";
        public UsersHandler() { }

        public async Task<ExitStatus> regiterUser(string data)
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

                if ((await userExists(username!)).status)
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

                return new ExitStatus();
            }
            catch (Exception ex) 
            {
                ExitStatus ret = new ExitStatus
                {
                    code = ExitStatus.Code.EXCEPTION,
                    message = ex.Message,
                    status = false
                };
                if (ex.Source == "System.Text.Json")
                {
                    // BAd request becouse this expetions means that
                    // the content of the recieved json is not correct
                    ret.code = ExitStatus.Code.BAD_REQUEST;
                }
                return ret;
            }
        }

        public async Task<ExitStatus> signinUser(string data)
        {
            try
            {
                JsonDocument parsedData = JsonDocument.Parse(data);
                var root = parsedData.RootElement;

                string? username = root.GetProperty(UsersDDBBStructure.USERNAME).GetString();
                string? password = root.GetProperty(UsersDDBBStructure.PASSWORD).GetString();


                Logger.UsersLogger.Debug("[login user] start - " + username);
                Logger.ConsoleLogger.Debug("[UsersAPI - loginUser] - Data rebuda: " + data);

                if (!(await userExists(username!)).status)
                {
                    return new ExitStatus
                    {
                        code = ExitStatus.Code.NOT_FOUND,
                        message = "User do not exist."
                    };
                }

                return new ExitStatus();
            }
            catch (Exception ex)
            {
                ExitStatus ret = new ExitStatus
                {
                    code = ExitStatus.Code.EXCEPTION,
                    message = ex.Message,
                    status = false
                };
                if (ex.Source == "System.Text.Json")
                {
                    // BAd request becouse this expetions means that
                    // the content of the recieved json is not correct
                    ret.code = ExitStatus.Code.BAD_REQUEST;
                }
                return ret;
            }
        }

        private async Task<ExitStatus> userExists(string username)
        {
            try
            {
                DDBBHandler dDBBHandler = DDBBHandler.getInstance();
                utils.ResultJson result = await dDBBHandler.find(DB_COLLECTION_NAME, UsersDDBBStructure.USERNAME, username);

                return new ExitStatus()
                {
                    code = result.code,
                    message = result.message,
                    status = result.status,
                };
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
