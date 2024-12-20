﻿using chatserver.DDBB;
using chatserver.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using chatserver.DDBB;
using chatserver.authentication;
using Microsoft.IdentityModel.Tokens;
using chatserver.server.APIs;

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
                result.result = TokenProvider.Instance.GenerateToken(userId, username);
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
