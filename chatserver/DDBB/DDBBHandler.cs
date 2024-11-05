using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.Json;
using System.Reflection.Metadata;
using System.Security.Policy;

namespace chatserver.DDBB
{
    internal class DDBBHandler
    {
        MongoClient client;
        private readonly string DATA_BASE_NAME = "chat";
        private static DDBBHandler instance = new DDBBHandler();

        public static DDBBHandler getInstance()
        {
            if (instance == null)
            {
                // Should never enter. But in case of error...
                instance = new DDBBHandler();
            }
            return instance; 
        }

        private DDBBHandler()
        {
            Logger.DataBaseLogger.Debug("Initializing DDBB");
            client = new MongoClient("mongodb://localhost:27017");
        }

        public async Task<utils.ExitStatus> write(string collectionName, JsonElement data)
        {
            try
            {
                Logger.DataBaseLogger.Debug("[write] - collection: " + collectionName);
                Logger.ConsoleLogger.Debug("[write] - collection: " + collectionName);
                // Accedir a la base de dades 'test'
                var database = client.GetDatabase(DATA_BASE_NAME);

                // Accedir a la col·lecció 'testddbb'
                var collection = database.GetCollection<BsonDocument>(collectionName);
                BsonDocument bsonDocument = BsonDocument.Parse(data.ToString());

                await collection.InsertOneAsync(bsonDocument);

                return new utils.ExitStatus { status = true, code = utils.ExitStatus.Code.OK };
            }
            catch (Exception ex)
            {
                return new utils.ExitStatus { status = false, code = utils.ExitStatus.Code.EXCEPTION, message = ex.Message };
            }
        }

        public async Task<utils.ResultJson> find(string collectionName, string key, string value)
        {
            try
            {
                var database = client.GetDatabase(DATA_BASE_NAME);
                var collection = database.GetCollection<BsonDocument>(collectionName);

                var filter = Builders<BsonDocument>.Filter.Eq(key, value);
                BsonDocument bResult = await collection.Find(filter).FirstOrDefaultAsync();

                return new utils.ResultJson
                {
                    code = bResult != null ? utils.ExitStatus.Code.OK : utils.ExitStatus.Code.NOT_FOUND,
                    status = bResult != null,
                    data = bResult != null ? JsonDocument.Parse(bResult.ToJson()) : null
                };
            }
            catch (Exception ex)
            {
                return new utils.ResultJson
                {
                    status = false,
                    code = utils.ExitStatus.Code.EXCEPTION,
                    message = ex.Message
                };
            }
        }
    }
}
