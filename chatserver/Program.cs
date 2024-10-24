using System.Net.WebSockets;
using System.Net;
using System.Text;
using System;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using static System.Runtime.InteropServices.JavaScript.JSType;
using chatserver.server;

namespace chatserver
{
    class Program()
    {
        static async Task Main(string[] args)
        {
            // Més endevant m'iteressarà no tenir l'await
            await Server.start();
        }
    }
}