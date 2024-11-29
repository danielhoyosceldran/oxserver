using chatserver.DDBB;
using chatserver.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using chatserver.DDBB;

namespace chatserver.authentication
{
    internal class SessionHandler
    {
        private static SessionHandler instance = new SessionHandler();
        private SessionHandler() { }
        public static SessionHandler Instance { get { return instance; } }

        private static int sessionsCounter = 0;

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

        public async static Task<ExitStatus> SignOutHandler(string sessionId)
        {
            try
            {
                DDBBHandler dDBBHandler = DDBBHandler.getInstance();
                ExitStatus result = await dDBBHandler.delete("sessions", "sessionId", sessionId);

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

        public static int GetSessionsCounter()
        {
            return ++sessionsCounter;
        }
    }
}
