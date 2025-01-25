using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.Json;
using chatserver.utils;
using log4net.Util;

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

                return new utils.ExitStatus { status = ExitCodes.OK, result = bsonDocument["_id"].ToString()};
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

                var filter = key == "_id"
                    ? Builders<BsonDocument>.Filter.Eq(key, new ObjectId(value))
                    : Builders<BsonDocument>.Filter.Eq(key, value);
                BsonDocument bResult = await collection.Find(filter).FirstOrDefaultAsync();

                return new ExitStatus
                {
                    status = bResult != null ? ExitCodes.OK : ExitCodes.NOT_FOUND,
                    result = bResult != null ? JsonDocument.Parse(bResult.ToJson()) : null
                };
            }
            catch (FormatException ex)
            {
                // Handle invalid ObjectId format
                return new ExitStatus
                {
                    status = ExitCodes.BAD_REQUEST,
                    exception = ex.Message,
                    message = "Invalid ObjectId format."
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

        public async Task<ExitStatus> UpdateField(string collectionName, string key, string value, string fieldToUpdate, object newValue)
        {
            try
            {
                // Accedeix a la base de dades i la col·lecció
                var database = client.GetDatabase(DATA_BASE_NAME);
                var collection = database.GetCollection<BsonDocument>(collectionName);

                // Filtra el document basant-se en el key i el value
                var filter = Builders<BsonDocument>.Filter.Eq(key, value);

                // Actualitza el camp específic amb el nou valor
                var update = Builders<BsonDocument>.Update.Set(fieldToUpdate, newValue);

                // Executa l'operació d'actualització
                var result = await collection.UpdateOneAsync(filter, update);

                // Comprova si s'ha actualitzat
                if (result.ModifiedCount > 0)
                {
                    return new ExitStatus
                    {
                        status = ExitCodes.OK,
                        message = $"Field '{fieldToUpdate}' updated successfully."
                    };
                }
                else
                {
                    return new ExitStatus
                    {
                        status = ExitCodes.NOT_FOUND,
                        message = $"Document with {key}={value} not found."
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

                // Converteix el valor en ObjectId si el camp és "_id"
                var filter = key == "_id"
                    ? Builders<BsonDocument>.Filter.Eq(key, ObjectId.Parse(value))
                    : Builders<BsonDocument>.Filter.Eq(key, value);

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

        public async Task<ExitStatus> RetrieveArrayField(
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

                // Build the filter to match the main document and the specific element within the array
                var filter = Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Eq(key, value),
                    Builders<BsonDocument>.Filter.ElemMatch(
                        arrayKey,
                        Builders<BsonDocument>.Filter.Eq(elementKey, elementValue)
                    )
                );

                // Project only the matching array element
                var projection = Builders<BsonDocument>.Projection
                    .ElemMatch(arrayKey, Builders<BsonDocument>.Filter.Eq(elementKey, elementValue));

                // Find the document with the specific array element
                var result = await collection.Find(filter).Project(projection).FirstOrDefaultAsync();

                if (result != null && result.Contains(arrayKey))
                {
                    // Extract the matching element from the array
                    var arrayElement = result[arrayKey].AsBsonArray.FirstOrDefault();
                    return new ExitStatus
                    {
                        status = ExitCodes.OK,
                        result = JsonSerializer.Deserialize<Dictionary<string, string>>(arrayElement.ToJson()) // Return the specific array element as JSON
                    };
                }
                else
                {
                    return new ExitStatus
                    {
                        status = ExitCodes.NOT_FOUND,
                        message = "Element not found in the array."
                    };
                }
            }
            catch (MongoException ex)
            {
                Logger.DataBaseLogger.Error($"MongoDB error: {ex.Message}");
                throw; // Re-throw the error for higher-level handling
            }
            catch (Exception ex)
            {
                Logger.DataBaseLogger.Error($"Unexpected error: {ex.Message}");
                throw;
            }
        }

        public async Task<ExitStatus> CustomRequest(FilterDefinition<BsonDocument> filter, UpdateDefinition<BsonDocument> update, string collectionName)
        {
            try
            {
                var database = client.GetDatabase(DATA_BASE_NAME);
                var collection = database.GetCollection<BsonDocument>(collectionName);

                // Apply the filter and update operations
                var result = await collection.UpdateManyAsync(filter, update);

                Logger.ConsoleLogger.Debug($"Matched {result.MatchedCount} documents and modified {result.ModifiedCount} documents.");

                return new ExitStatus()
                {
                    status = result.MatchedCount > 0 ? ExitCodes.OK : ExitCodes.NOT_FOUND,
                };
            }
            catch (Exception ex)
            {
                Logger.ConsoleLogger.Error($"Error executing CustomRequest: {ex.Message}");
                throw;
            }
        }

        public async Task<ExitStatus> RetrieveLastArrayElement(string collectionName, string key, string value, string arrayField)
        {
            try
            {
                var database = client.GetDatabase(DATA_BASE_NAME);
                var collection = database.GetCollection<BsonDocument>(collectionName);

                // Create the filter to match the document
                var filter = key == "_id"
                    ? Builders<BsonDocument>.Filter.Eq(key, ObjectId.Parse(value))
                    : Builders<BsonDocument>.Filter.Eq(key, value);

                // Sort the array in descending order by index and limit to 1
                var projection = Builders<BsonDocument>.Projection
                    .Slice(arrayField, -1); // Get the last element of the array

                // Query the document with the specified filter and projection
                var result = await collection.Find(filter).Project(projection).FirstOrDefaultAsync();

                // Check if the result and arrayField exist
                if (result != null && result.Contains(arrayField))
                {
                    var lastElement = result[arrayField].AsBsonArray.FirstOrDefault();
                    return new ExitStatus
                    {
                        status = ExitCodes.OK,
                        result = lastElement.ToJson()
                    };
                }
                else
                {
                    return new ExitStatus
                    {
                        status = ExitCodes.NOT_FOUND,
                        message = "Array field not found or document does not exist."
                    };
                }
            }
            catch (Exception ex)
            {
                Logger.DataBaseLogger.Error($"Error retrieving last element of the array: {ex.Message}");
                return new ExitStatus
                {
                    status = ExitCodes.EXCEPTION,
                    exception = ex.Message
                };
            }
        }

    }
}
