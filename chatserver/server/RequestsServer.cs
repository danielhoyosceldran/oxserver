using chatserver.server.APIs;
using log4net.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace chatserver.server
{
    internal class RequestsServer
    {
        private static int Port = 8081;

        private static HttpListener _listener;
        private static UsersAPI usersAPI = new UsersAPI();

        public static async Task start()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://*:" + Port.ToString() + "/");
            _listener.Start();
            Receive();
        }

        public void Stop()
        {
            _listener.Stop();
        }

        private static void Receive()
        {
            _listener.BeginGetContext(new AsyncCallback(ListenerCallback), _listener);
        }

        private async static void ListenerCallback(IAsyncResult result)
        {
            if (_listener.IsListening)
            {
                var context = _listener.EndGetContext(result);
                var request = context.Request;

                // TODO: comprovar de quin tipus de request es tracta i enviar-ho a un thread a part per a que es gestioni a part
                // El thread s'haùrpa de tancar correctament
                if (request.HttpMethod == "POST")
                {
                    // Aquí pot haber problemes en cas de tenir nulls
                    // no puc simplement fer "return". He de fer el recieve.
                    string? absolutePath = request.Url?.AbsolutePath;
                    string[]? segments = request.Url?.Segments.Select(s => s.Trim('/')).ToArray();

                    if (segments[0] == "/users")
                    {
                        handleUserReqeusts(request);
                    }
                }

                var response = context.Response;
                response.StatusCode = (int)HttpStatusCode.OK;
                response.ContentType = "text/plain";
                string responseBody = "revieved";
                byte[] buffer = Encoding.UTF8.GetBytes(responseBody);
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();
                Console.WriteLine($"{request.HttpMethod} {request.Url} - OK");


                Receive();
            }
        }

        private static async void handleUserReqeusts(HttpListenerRequest? request)
        {
            if (request.HasEntityBody)
            {
                var body = request.InputStream;
                var encoding = request.ContentEncoding;
                var reader = new StreamReader(body, encoding);
                if (request.ContentType != null)
                {
                    Logger.RequestServerLogger.Debug("Client data content type {0}" + request.ContentType);
                }
                Logger.RequestServerLogger.Debug("Client data content length {0}" + request.ContentLength64);

                // reding data
                string recievedData = reader.ReadToEnd();
                await usersAPI.regiterUser(recievedData);
                reader.Close();
                body.Close();
            }
        }
    }
}
