using System.Net.WebSockets;
using System.Net;
using System.Text;
using System.Text.Json;
using MongoDB.Bson;
using System;

namespace chatserver.server
{
    class ChatServer
    {
        // Creem el punt d'entrada
        private static readonly string serverAddress = "http://*:5000/";
        private static Dictionary<string, WebSocket> webSockets = new Dictionary<string, WebSocket>();

        public static async Task start()
        {
            // Crear un HttpListener per rebre sol·licituds HTTP i WebSocket
            HttpListener httpListener = new HttpListener();
            httpListener.Prefixes.Add(serverAddress);
            httpListener.Start();
            Console.WriteLine("Servidor WebSocket en execució a: " + serverAddress);

            // Bucle infinit per acceptar connexions
            // Es queda aquí fins que algun client es conecta
            while (true)
            {
                // Acceptar sol·licitud de connexió
                HttpListenerContext context = await httpListener.GetContextAsync();

                // Comprovar si la sol·licitud és una sol·licitud WebSocket
                if (context.Request.IsWebSocketRequest)
                {
                    Logger.WebSocketsServerLogger.Info("Socket request");
                    Logger.ConsoleLogger.Info("Socket request");
                    // Gestionar connexió WebSocket
                    // La gestió es fa a un thread a part per a poder seguir acceptant connexions
                    // TODO - Mirar si quest wharning (el de que cal un "await")
                    HandleWebSocketConnectionAsync(context);
                }
                else
                {
                    // Retornar una resposta HTTP 400 si no és una sol·licitud WebSocket
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
        }

        // Funció per gestionar la connexió WebSocket
        private static async Task HandleWebSocketConnectionAsync(HttpListenerContext context)
        {
            // Acceptar la sol·licitud WebSocket i obtenir el WebSocket
            HttpListenerWebSocketContext wsContext = null;
            try
            {
                wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
                Console.WriteLine("[server] - new connection accepted");
            }
            catch (Exception e)
            {
                Console.WriteLine("Error en acceptar connexió WebSocket: " + e.Message);
                context.Response.StatusCode = 500;
                context.Response.Close();
                return;
            }

            // Obtenir el WebSocket actiu
            WebSocket webSocket = wsContext.WebSocket;

            // save the user connection
            webSockets.Add(webSockets.Count.ToString(), webSocket);

            // Bucle per rebre missatges del client
            // 4k caràcters
            byte[] buffer = new byte[4096];
            while (webSocket.State == WebSocketState.Open)
            {
                try
                {
                    // Esperar missatge del client
                    WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    // Si la connexió és tancada pel client
                    if (result.MessageType == WebSocketMessageType.Close)
                    { 
                        Console.WriteLine("[server] - A client has closed the connection");
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
                    }

                    // If the message is text...
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        // Read the recieved message
                        string clientMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        Console.WriteLine("Message: " + clientMessage);

                        using JsonDocument doc = JsonDocument.Parse(clientMessage);
                        JsonElement root = doc.RootElement;

                        string? type = root.GetProperty("type").GetString();

                        if (type == "text")
                        {
                            string? message = root.GetProperty("text").GetString();
                            // Send reply
                            foreach (KeyValuePair<string, WebSocket> ws in webSockets)
                            {
                                SendQueuedMessage(ws.Value, message!);
                            }
                        }




                        // Clean the buffer to recieve more messages
                        Array.Clear(buffer, 0, buffer.Length);
                    }
                }
                catch (WebSocketException e)
                {
                    Console.WriteLine("Error en la comunicació WebSocket: " + e.Message);
                    break;
                }
            }

            // Close the socket (if we exit the loop)
            if (webSocket.State != WebSocketState.Closed)
            {

                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connexió finalitzada", CancellationToken.None);
            }

            webSocket.Dispose();
        }

        private static async Task sendMessage(WebSocket webSocket, string clientMessage, string from)
        {
            string responseMessage = from + ": " + clientMessage;
            byte[] responseBytes = Encoding.UTF8.GetBytes(responseMessage);
            await webSocket.SendAsync(new ArraySegment<byte>(responseBytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private static void SendQueuedMessage(WebSocket webSocket, string message)
        {
            // Implementa una cua per assegurar que només es realitza un enviament alhora
            lock (webSocket)
            {
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                webSocket.SendAsync(
                    new ArraySegment<byte>(messageBytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );
            }
        }
    }
}