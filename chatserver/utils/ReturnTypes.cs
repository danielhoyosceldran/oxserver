using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace chatserver.utils
{
    public class ResultJson
    {
        public bool status { get; set; }
        public JsonDocument? data { get; set; }
    }

    public class ResultString
    {
        public bool status { get; set; }
        public string? data { get; set; }
    }
}
