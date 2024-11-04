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
        public UsersAPI() { }

        public static bool regiterUser(string data)
        {
            JsonDocument parsedData = JsonDocument.Parse(data);
            var root = parsedData.RootElement;

            string name = root.GetProperty("name").GetString();
            string username = root.GetProperty("username").GetString();
            string password = root.GetProperty("password").GetString();

            DDBBHandler dDBBHandler = DDBBHandler.Instance;
            dDBBHandler.write("users", root);

            Logger.UsersLogger.Debug("[register user] start - " + username);
            Logger.ConsoleLogger.Debug("[UsersAPI - registerUser] - Data rebuda: " + data);

            return true;
        }
    }
}
