using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace chatserver.utils
{
    /// <summary>
    /// status, 
    /// code, 
    /// message, 
    /// </summary>
    public class ExitStatus
    {
        /// <summary>
        /// OK = 0,
        /// UNKNOWN_ERROR = 1,
        /// EXCEPTION = 2,
        /// ERROR = 3,
        /// NOT_FOUND = 404,
        /// NOT_AUTHORIZED = 401,
        /// </summary>
        public enum Code : ushort
        {
            OK = 0,
            UNKNOWN_ERROR = 1,
            EXCEPTION = 2,
            ERROR = 3,
            NOT_FOUND = 404,
            NOT_AUTHORIZED = 401,
        }
        public bool status { get; set; } = true;
        public Code code { get; set; } = Code.OK;
        public string message { get; set; } = "";
    }

    public class ResultJson : ExitStatus
    {
        public JsonDocument? data { get; set; } = null;
    }
}
