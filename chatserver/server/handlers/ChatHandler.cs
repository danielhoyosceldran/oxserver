using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using chatserver.DDBB;
using chatserver.utils;

namespace chatserver.server.APIs
{
    internal class ChatHandler
    {
        // conversations
        public static async Task<ExitStatus> CreateConversation(string username, string contactUsername, bool privateConversation = true)
        {
            try
            {
                DDBBHandler ddbb = DDBBHandler.Instance;

                // TODO: Check if the conversation already exist.
                // If yes: check if is added to each user and then act depending the case.
                // In an ideal world this is not necessary. Let's trust.

                // null fields do not appear in the final json
                var options = new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };

                List<string> participants = new List<string>();

                if (privateConversation)
                {
                    participants.Add(username);
                    participants.Add(contactUsername);
                }

                var newConversation = JsonDocument.Parse(
                    JsonSerializer.Serialize(new
                    {
                        type = privateConversation ? "private" : "group",
                        participants = privateConversation ? participants : null,
                        messages = new List<object>()
                    }, options)
                ).RootElement;

                ExitStatus creteConversationResult = await ddbb.write(DataBaseCollections.CONVERSATIONS, newConversation);

                return creteConversationResult;
            }
            catch (Exception ex)
            {
                return new ExitStatus
                {
                    status = ExitCodes.EXCEPTION,
                    exception = ex.Message,
                    message = "Something went wrong creating the conversation"
                };
            }
        }

        public static async Task<ExitStatus> RetrieveMessages(string username, string conversationObjective)
        {
            try
            {
                DDBBHandler ddbb = DDBBHandler.Instance;
                ExitStatus searchResult = await ddbb.RetrieveArrayField(
                    DataBaseCollections.USERS,
                    UsersDDBBStructure.USERNAME,
                    username,
                    UsersDDBBStructure.CONVERSATIONS,
                    UsersDDBBStructure.CHAT,
                    conversationObjective
                );

                if (searchResult.status != ExitCodes.OK) return new ExitStatus { status = ExitCodes.ERROR, message = "Error finding messages" };

                Dictionary<string, string> conversationDictionary = (Dictionary<string, string>)searchResult.result!;

                string conversationId = conversationDictionary["conversationId"];

                ExitStatus messagesResult = await ddbb.find(DataBaseCollections.CONVERSATIONS, "_id", conversationId);

                if (messagesResult.status != ExitCodes.OK) return new ExitStatus { status = ExitCodes.ERROR, message = "Error exrtacting messages" };

                JsonDocument messageObject = (JsonDocument)messagesResult.result!;
                //var messages = messageObject.RootElement.GetProperty("messages");

                // TODO: Recuperar el id de l'objecte i posarlo com a "id" en comtes de _id.$oid, per a poder recuperar-lo correctament a javascript

                string messageObjectString = JsonSerializer.Serialize(messageObject.RootElement);

                messagesResult.result = messageObjectString;
                //messagesResult.result = messages.ToString();


                return messagesResult;
            }
            catch (Exception ex)
            {
                return new ExitStatus
                {
                    status = ExitCodes.EXCEPTION,
                    exception = ex.ToString(),
                    message = ex.Message
                };
            }
        }

        public static async Task<ExitStatus> AddMessage(JsonElement message)
        {
            DDBBHandler ddbb = DDBBHandler.Instance;
            //ExitStatus writeResult = await ddbb.AddToArrayField(DataBaseCollections.CONVERSATIONS, )

            return new ExitStatus();
        }

    }
}
