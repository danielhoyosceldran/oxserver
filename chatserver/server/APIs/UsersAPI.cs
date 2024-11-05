using chatserver.DDBB;
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

        public async Task<bool> regiterUser(string data)
        {
            // TODO: arreglar tema de possibles null
            JsonDocument parsedData = JsonDocument.Parse(data);
            var root = parsedData.RootElement;

            string name = root.GetProperty(Users.NAME).GetString();
            string username = root.GetProperty(Users.USERNAME).GetString();
            string password = root.GetProperty(Users.PASSWORD).GetString();

            if (await userExists(username))
            {
                Logger.ConsoleLogger.Debug("L'usuari ja existeix");
                return false;
            }

            DDBBHandler dDBBHandler = DDBBHandler.getInstance();
            dDBBHandler.write("users", root);

            Logger.UsersLogger.Debug("[register user] start - " + username);
            Logger.ConsoleLogger.Debug("[UsersAPI - registerUser] - Data rebuda: " + data);

            return true;
        }

        private async Task<bool> userExists(string username)
        {
            DDBBHandler dDBBHandler = DDBBHandler.getInstance();
            utils.ResultJson result = await dDBBHandler.find(DB_COLLECTION_NAME, Users.USERNAME, username);
            
            return result.status;
        }
    }
}
