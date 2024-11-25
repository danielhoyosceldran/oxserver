using chatserver.DDBB;
using chatserver.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace chatserver.authentication
{
    internal class SessionHandler
    {
        public async Task<ExitStatus> isSessionActive(string sessionId)
        {
            try
            {
                DDBB.DDBBHandler dDBBHandler = DDBB.DDBBHandler.getInstance();
                ExitStatus result = await dDBBHandler.find("sessions", "sessionId", sessionId);

                return new ExitStatus()
                {
                    status = result.status,
                    message = result.message,
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
