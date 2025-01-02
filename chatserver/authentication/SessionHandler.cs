using chatserver.DDBB;
using chatserver.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using chatserver.authentication;
using Microsoft.IdentityModel.Tokens;
using chatserver.server.APIs;
using System.Text.Json;

namespace chatserver.authentication
{
    internal class SessionHandler
    {
        private static SessionHandler instance = new SessionHandler();
        private SessionHandler() { }
        public static SessionHandler Instance { get { return instance; } }
        private static readonly string DB_COLLECTION_NAME = "sessions";

        private static int sessionsCounter = 0;

        // for sessions with cookies
        public async static Task<ExitStatus> IsSessionActive(string sessionId)
        {
            try
            {
                DDBBHandler dDBBHandler = DDBBHandler.Instance;
                ExitStatus result = await dDBBHandler.find("sessions", "sessionId", sessionId);

                return new ExitStatus()
                {
                    status = result.status,
                    message = result.message,
                    result = result.status == ExitCodes.OK ? true : false
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

        public async static Task<ExitStatus> UpdateSession(string username, TokensStruct tokens)
        {
            // TODO: COMPLETAR
            var ddbb = DDBBHandler.Instance;
            await ddbb.delete("sessions", "username", username);

            // add new refresh token
            var jsonDocument = new
            {
                username = username,
                refreshToken = tokens.refreshToken,
                accessToken = tokens.accessToken
            };
            var jsonElement = JsonDocument.Parse(JsonSerializer.Serialize(jsonDocument)).RootElement;
            await ddbb.write("sessions", jsonElement);

            return new ExitStatus();
        }

        public async static Task<ExitStatus> GetUsernameFromAccessToken(string accessToken)
        {
            try
            {
                // Obtenir la instància de DDBBHandler
                DDBBHandler ddbbHandler = DDBBHandler.Instance;

                // Consultar la base de dades per trobar el document amb l'accessToken donat
                var result = await ddbbHandler.RetrieveField(
                    collectionName: "sessions",
                    key: "accessToken",
                    value: accessToken,
                    fieldToRetrieve: "username"
                );

                // Comprovar el resultat de la cerca
                if (result.status == ExitCodes.OK && result.result != null)
                {
                    // Retornar l'ExitStatus amb el username trobat
                    return new ExitStatus
                    {
                        status = ExitCodes.OK,
                        message = "Username retrieved successfully.",
                        result = result.result
                    };
                }
                else if (result.status == ExitCodes.NOT_FOUND)
                {
                    // No s'ha trobat cap document amb l'accessToken
                    return new ExitStatus
                    {
                        status = ExitCodes.NOT_FOUND,
                        message = "Access token not found."
                    };
                }
                else
                {
                    // Error desconegut
                    return new ExitStatus
                    {
                        status = ExitCodes.ERROR,
                        message = "An error occurred while retrieving the username."
                    };
                }
            }
            catch (Exception ex)
            {
                // Gestió d'errors inesperats
                Logger.DataBaseLogger.Error($"[GetUsernameFromAccessToken] Unexpected error: {ex.Message}");
                return new ExitStatus
                {
                    status = ExitCodes.EXCEPTION,
                    exception = ex.Message
                };
            }
        }

        private static async Task UpdateAccessTokenInDdbb(string accessToken, string username)
        {
            var dbHandler = DDBBHandler.Instance;
            string collectionName = "sessions";
            string key = "username";
            string fieldToUpdate = "accessToken";
           
            ExitStatus result = await dbHandler.UpdateField(collectionName, key, username, fieldToUpdate, accessToken);
        }


        public async static Task<ExitStatus> RefreshSession(string token, string username)
        {
            ExitStatus result = new ExitStatus()
            {
                status = ExitCodes.ERROR,
                message = "Refresh token not valid"
            };

            // This is only if we serve signed refresh tokens
            // We are not now.
            //ExitStatus tokenValidationResult = CustomValidateToken(token, false);

            DDBBHandler dDBBHandler = DDBBHandler.Instance;
            string storedRefreshToken = (string) (await dDBBHandler.RetrieveField(DB_COLLECTION_NAME, "username", username, "refreshToken")).result!;
            bool tokenValidationResult = TokenProvider.Instance.ValidateRefreshToken(token, storedRefreshToken);
            if (tokenValidationResult)
            {
                string userId = (string) (await UsersHandler.Instance.GetUserId(username)).result!;
                result.status = ExitCodes.OK;
                result.message = "Refresh Token valid";
                string accessToken = TokenProvider.Instance.GenerateToken(userId, username);
                await UpdateAccessTokenInDdbb(accessToken, username);
                result.result = accessToken;
            }
            return result;
        }

        public static ExitStatus CustomValidateToken(string token, bool isAccessToken = true)
        {
            Logger.ConsoleLogger.Debug("[CustomValidateToken] - Start");
            var returnval = TokenProvider.Instance.ValidateToken(token, isAccessToken);
            ExitCodes res = returnval == null
                ? ExitCodes.UNAUTHORIZED
                : returnval.Identity!.IsAuthenticated ? ExitCodes.OK : ExitCodes.UNAUTHORIZED;
            Logger.ConsoleLogger.Debug($"[CustomValidateToken] - validation result: {res.ToString()}");
            Logger.ConsoleLogger.Debug("[CustomValidateToken] - End");
            return new ExitStatus()
            {
                status = returnval == null
                ? ExitCodes.UNAUTHORIZED
                : returnval.Identity!.IsAuthenticated ? ExitCodes.OK : ExitCodes.UNAUTHORIZED,
                result = returnval == null ? false : returnval.Identity!.IsAuthenticated
            };
        }

        public async static Task<ExitStatus> GetTokens(string username)
        {
            string userId = (string)(await UsersHandler.Instance.GetUserId(username)).result!;
            TokensStruct tokens = new TokensStruct();
            tokens.accessToken = TokenProvider.Instance.GenerateToken(userId, username);
            tokens.refreshToken = TokenProvider.Instance.GenerateRefreshToken();

            await UpdateSession(username, tokens);

            return new ExitStatus()
            {
                result = tokens
            };
        }

        public async static Task<ExitStatus> deleteSession(string username)
        {
            DDBBHandler dDBBHandler = DDBBHandler.Instance;
            return await dDBBHandler.delete(DB_COLLECTION_NAME, "username", username);
        }

        // Only for cookies session id
        public static int GetSessionsCounter()
        {
            return ++sessionsCounter;
        }

        public async static Task<ExitStatus> SignOut(string sessionId)
        {
            // TODO
            // pendent d'adabtar a amb refresh tokens
            try
            {
                DDBBHandler dDBBHandler = DDBBHandler.Instance;
                ExitStatus result = await dDBBHandler.delete("sessions", "refreshToken", sessionId);

                return new ExitStatus()
                {
                    status = result.status,
                    message = result.message,
                    result = result.status == ExitCodes.OK ? true : false
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
    }
}
