using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace chatserver.utils
{
    public static class Utils
    {
        public static List<string> GetUrlRoutes(Uri url)
        {
            List<string>? segments = url.Segments.Skip(1).Select(s => s.Trim('/')).ToList();
            return segments;
        }
    }
}
