using chatserver.DDBB;
using chatserver.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using chatserver.DDBB;
using chatserver.authentication;

namespace chatserver.authentication
{
    internal class SessionHandler
    {
        private static SessionHandler instance = new SessionHandler();
        private SessionHandler() { }
        public static SessionHandler Instance { get { return instance; } }

        private static int sessionsCounter = 0;

        // for sessions with cookies
        public async static Task<ExitStatus> IsSessionActive(string sessionId)
        {
            try
            {
                DDBBHandler dDBBHandler = DDBBHandler.getInstance();
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

        public async static Task<ExitStatus> RefreshSession(string token)
        {
            return new ExitStatus()
            {
                result = "token"
            };
        }

        public async static Task<ExitStatus> validateAaccessToken(string token)
        {
            var returnval = TokenProvider.Instance.ValidateToken(token);
            return new ExitStatus()
            {
                status = returnval == null
                ? ExitCodes.UNAUTHORIZED
                : returnval.Identity!.IsAuthenticated ? ExitCodes.OK : ExitCodes.UNAUTHORIZED,
                result = returnval == null ? false : returnval.Identity!.IsAuthenticated
            };
        }

        public async static Task<ExitStatus> GetTokens(string userId, string username)
        {
            TokensStruct tokens = new TokensStruct();
            tokens.accessToken = TokenProvider.Instance.GenerateToken(userId, username);
            tokens.refreshToken = TokenProvider.Instance.GenerateRefreshToken();
            return new ExitStatus()
            {
                result = tokens
            };
        }


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
                DDBBHandler dDBBHandler = DDBBHandler.getInstance();
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
