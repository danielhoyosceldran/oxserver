using System.Net.WebSockets;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;

namespace chatserver.server
{
    class ChatServer
    {
        private static readonly string serverAddress = "http://*:5000/";
        private static ConcurrentDictionary<string, WebSocket> webSockets = new ConcurrentDictionary<string, WebSocket>();

        public static async Task Start()
        {
            HttpListener httpListener = new HttpListener();
            httpListener.Prefixes.Add(serverAddress);
            httpListener.Start();
            Console.WriteLine($"Servidor WebSocket en execució a: {serverAddress}");

            while (true)
            {
                HttpListenerContext context = await httpListener.GetContextAsync();

                if (context.Request.IsWebSocketRequest)
                {
                    _ = HandleWebSocketConnectionAsync(context);
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
        }

        private static async Task HandleWebSocketConnectionAsync(HttpListenerContext context)
        {
            HttpListenerWebSocketContext wsContext;
            try
            {
                wsContext = await context.AcceptWebSocketAsync(null);
            }
            catch (Exception e)
            {
                Logger.ConsoleLogger.Debug($"Error acceptant connexió WebSocket: {e.Message}");
                context.Response.StatusCode = 500;
                context.Response.Close();
                return;
            }

            string? username = context.Request.QueryString["username"];
            if (string.IsNullOrEmpty(username))
            {
                Logger.ConsoleLogger.Debug("Username no vàlid");
                await wsContext.WebSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Username requerit", CancellationToken.None);
                return;
            }

            WebSocket webSocket = wsContext.WebSocket;
            webSockets[username] = webSocket;

            Logger.ConsoleLogger.Debug($"[server] - Nova connexió: {username}");

            byte[] buffer = new byte[4096];
            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Console.WriteLine($"[server] - {username} ha tancat la connexió");
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connexió tancada", CancellationToken.None);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string clientMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        Logger.ConsoleLogger.Debug($"Missatge rebut de {username}: {clientMessage}");
                        await ProcessMessage(username, clientMessage);
                    }
                }
            }
            catch (WebSocketException e)
            {
                Console.WriteLine($"Error de WebSocket per a {username}: {e.Message}");
            }
            finally
            {
                webSockets.TryRemove(username, out _);
                webSocket.Dispose();
            }
        }

        private static async Task ProcessMessage(string from, string message)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(message);
                JsonElement root = doc.RootElement;

                string? type = root.GetProperty("type").GetString();
                if (type == "text")
                {
                    string? text = root.GetProperty("content").GetString();
                    string? to = root.GetProperty("to").GetString();

                    if (!string.IsNullOrEmpty(to) && webSockets.TryGetValue(to, out WebSocket toSocket) && webSockets.TryGetValue(from, out WebSocket fromSocket))
                    {
                        await SendMessageAsync(toSocket, message);
                        await SendMessageAsync(fromSocket, message);
                    }
                    else
                    {
                        Logger.ConsoleLogger.Debug($"Destinatari no trobat: {to}");
                    }
                }
            }
            catch (JsonException e)
            {
                Console.WriteLine($"Error processant missatge JSON: {e.Message}");
            }
        }

        private static async Task SendMessageAsync(WebSocket webSocket, string message)
        {
            if (webSocket.State == WebSocketState.Open)
            {
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                await webSocket.SendAsync(
                    new ArraySegment<byte>(messageBytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );
            }
        }
    }
}
