﻿using System.Net.WebSockets;
using System.Net;
using System.Text;
using System;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using static System.Runtime.InteropServices.JavaScript.JSType;
using chatserver.server;
using chatserver.server.APIs;

namespace chatserver
{
    class Program()
    {
        static async Task Main(string[] args)
        {
            _ = API.Start();
            _ = ChatServer.Start();
            await UsersHandler.Instance.AddContactOrGroup("danielfurats", "guest0");
            await UsersHandler.Instance.AddContactOrGroup("danielfurats", "#gId-ff9901");

            // keep the programm running always
            await Task.Delay(-1);
        }
    }
}