using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.Json;
using chatserver.utils;

namespace chatserver.DDBB
{
    internal class DDBBHandler
    {
        MongoClient client;
        private readonly string DATA_BASE_NAME = "chat";
        private static DDBBHandler instance = new DDBBHandler();
        public static DDBBHandler Instance { get { return instance; } }

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

                return new utils.ExitStatus { status = ExitCodes.OK };
            }
            catch (Exception ex)
            {
                return new utils.ExitStatus { status = ExitCodes.EXCEPTION, exception = ex.Message };
            }
        }

        public async Task<ExitStatus> find(string collectionName, string key, string value)
        {
            try
            {
                var database = client.GetDatabase(DATA_BASE_NAME);
                var collection = database.GetCollection<BsonDocument>(collectionName);

                var filter = Builders<BsonDocument>.Filter.Eq(key, value);
                BsonDocument bResult = await collection.Find(filter).FirstOrDefaultAsync();

                return new ExitStatus
                {
                    status = bResult != null ? ExitCodes.OK : ExitCodes.NOT_FOUND,
                    result = bResult != null ? JsonDocument.Parse(bResult.ToJson()) : null
                };
            }
            catch (Exception ex)
            {
                return new ExitStatus
                {
                    status = ExitCodes.EXCEPTION,
                    exception = ex.Message
                };
            }
        }

        public async Task<ExitStatus> RetrieveField(string collectionName, string key, string value, string fieldToRetrieve)
        {
            try
            {
                // Accedim a la base de dades i la col·lecció
                var database = client.GetDatabase(DATA_BASE_NAME);
                var collection = database.GetCollection<BsonDocument>(collectionName);

                // Construïm el filtre de cerca
                var filter = Builders<BsonDocument>.Filter.Eq(key, value);

                // Especifica el camp que vols recuperar
                var projection = Builders<BsonDocument>.Projection.Include(fieldToRetrieve).Exclude("_id");

                // Busca el document i aplica la projecció
                var bResult = await collection.Find(filter).Project(projection).FirstOrDefaultAsync();

                // Comprovem si el resultat existeix
                if (bResult != null && bResult.Contains(fieldToRetrieve))
                {
                    return new ExitStatus
                    {
                        status = ExitCodes.OK,
                        result = bResult[fieldToRetrieve].ToString()
                    };
                }
                else
                {
                    return new ExitStatus
                    {
                        status = ExitCodes.NOT_FOUND,
                        message = $"Field '{fieldToRetrieve}' not found or document does not exist"
                    };
                }
            }
            catch (Exception ex)
            {
                return new ExitStatus
                {
                    status = ExitCodes.EXCEPTION,
                    exception = ex.Message
                };
            }
        }

        public async Task<ExitStatus> delete(string collectionName, string key, string value)
        {
            try
            {
                Logger.DataBaseLogger.Debug($"[delete] - collection: {collectionName}, key: {key}, value: {value}");
                Logger.ConsoleLogger.Debug($"[delete] - collection: {collectionName}, key: {key}, value: {value}");

                var database = client.GetDatabase(DATA_BASE_NAME);
                var collection = database.GetCollection<BsonDocument>(collectionName);

                var filter = Builders<BsonDocument>.Filter.Eq(key, value);
                var result = await collection.DeleteOneAsync(filter);

                if (result.DeletedCount > 0)
                {
                    return new ExitStatus
                    {
                        status = ExitCodes.OK,
                        message = $"Document with {key}={value} deleted successfully."
                    };
                }
                else
                {
                    return new ExitStatus
                    {
                        status = ExitCodes.NOT_FOUND,
                        message = $"No document found with {key}={value}."
                    };
                }
            }
            catch (Exception ex)
            {
                return new ExitStatus
                {
                    status = ExitCodes.EXCEPTION,
                    exception = ex.Message
                };
            }
        }

        public async Task<ExitStatus> AddToArrayField(string collectionName, string key, string value, string arrayField, JsonElement newItem)
        {
            try
            {
                // Accedeix a la base de dades i a la col·lecció
                var database = client.GetDatabase(DATA_BASE_NAME);
                var collection = database.GetCollection<BsonDocument>(collectionName);

                // Filtra el document basant-se en el key i el value
                var filter = Builders<BsonDocument>.Filter.Eq(key, value);

                // Crea l'operació de push per afegir l'element a l'array
                var update = Builders<BsonDocument>.Update.Push(arrayField, BsonDocument.Parse(newItem.ToString()));

                // Actualitza el document
                var result = await collection.UpdateOneAsync(filter, update);

                // Comprova si s'ha actualitzat
                if (result.ModifiedCount > 0)
                {
                    return new ExitStatus
                    {
                        status = ExitCodes.OK,
                        message = $"Successfully added to {arrayField}."
                    };
                }
                else
                {
                    return new ExitStatus
                    {
                        status = ExitCodes.NOT_FOUND,
                        message = $"Document with {key}={value} not found or {arrayField} does not exist."
                    };
                }
            }
            catch (Exception ex)
            {
                return new ExitStatus
                {
                    status = ExitCodes.EXCEPTION,
                    exception = ex.Message
                };
            }
        }

        public async Task<bool> FindInArray(
            string collectionName, 
            string key, 
            string value, 
            string arrayKey, 
            string elementKey, 
            string elementValue)
        {
            try
            {
                var database = client.GetDatabase(DATA_BASE_NAME);
                var collection = database.GetCollection<BsonDocument>(collectionName);

                // Construir el filtre
                var filter = Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Eq(key, value),
                    Builders<BsonDocument>.Filter.ElemMatch(
                        arrayKey,
                        Builders<BsonDocument>.Filter.Eq(elementKey, elementValue)
                    )
                );

                // Cercar el document
                var result = await collection.Find(filter).FirstOrDefaultAsync();
                return result != null;
            }
            catch (MongoException ex)
            {
                Logger.DataBaseLogger.Error($"MongoDB error: {ex.Message}");
                throw; // Llança l'error per a gestió superior
            }
            catch (Exception ex)
            {
                Logger.DataBaseLogger.Error($"Unexpected error: {ex.Message}");
                throw;
            }
        }


    }
}
