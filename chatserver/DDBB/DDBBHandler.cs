using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson; // Per treballar amb documents BSON
using MongoDB.Driver; // Per accedir a la funcionalitat de MongoDB
using System.Text.Json;
using System.Reflection.Metadata;
using System.Security.Policy;

namespace chatserver.DDBB
{
    internal class DDBBHandler
    {
        MongoClient client;
        private readonly string DATA_BASE_NAME = "chat";
        public static DDBBHandler Instance = new DDBBHandler();
        private DDBBHandler()
        {
            Logger.DataBaseLogger.Debug("Initializing DDBB");
            client = new MongoClient("mongodb://localhost:27017");
        }

        public async void write(string table, JsonElement data)
        {
            Logger.DataBaseLogger.Debug("[write] - table: " + table);
            Logger.ConsoleLogger.Debug("[write] - table: " + table);
            // Accedir a la base de dades 'test'
            var database = client.GetDatabase(DATA_BASE_NAME);

            // Accedir a la col·lecció 'testddbb'
            var collection = database.GetCollection<BsonDocument>(table);
            BsonDocument bsonDocument = BsonDocument.Parse(data.ToString());

            await collection.InsertOneAsync(bsonDocument);
        }

        public async Task<JsonDocument?> find(string table, string key, string value)
        {
            // Accedir a la base de dades 'test'
            var database = client.GetDatabase(DATA_BASE_NAME);

            // Accedir a la col·lecció 'testddbb'
            var collection = database.GetCollection<BsonDocument>(table);

            var filter = Builders<BsonDocument>.Filter.Eq(key, value); // Filtre per nom = "Joan"
            BsonDocument result = await collection.Find(filter).FirstOrDefaultAsync();

            return result == null 
                ? null 
                : JsonDocument.Parse(result.ToJson());
        }
    }
}
