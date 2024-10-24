using System.Net.WebSockets;
using System.Net;
using System.Text;


namespace chatserver.server
{
    class Server
    {
        // Especifica l'adreça IP i el port on el servidor WebSocket escoltarà
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
            // Es queda aquí fins que algun client es conecta.
            while (true)
            {
                // Acceptar sol·licitud de connexió
                HttpListenerContext context = await httpListener.GetContextAsync();

                // Comprovar si la sol·licitud és una sol·licitud WebSocket
                if (context.Request.IsWebSocketRequest)
                {
                    // Gestionar connexió WebSocket
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
                Console.WriteLine("Connexió WebSocket acceptada.");
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
            webSockets.Add(webSockets.Count.ToString(), webSocket);

            // Bucle per rebre missatges del client
            byte[] buffer = new byte[1024];
            while (webSocket.State == WebSocketState.Open)
            {
                try
                {
                    // Esperar missatge del client
                    WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    // Si la connexió és tancada pel client
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Console.WriteLine("El client ha tancat la connexió.");
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connexió tancada pel client", CancellationToken.None);
                    }
                    // If the message is text...
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        // Read the recieved message
                        string clientMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        Console.WriteLine("Missatge rebut del client: " + clientMessage);

                        // Send reply
                        foreach (KeyValuePair<string, WebSocket> ws in webSockets)
                        {
                            sendMessage(ws.Value, clientMessage, ws.Key);
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

    }

}