using chatserver.server.APIs;
using chatserver.utils;
using log4net.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Xml.XPath;

namespace chatserver.server
{
    internal class API
    {
        private static int Port = 8081;

        private static HttpListener _listener;
        private static UsersHandler usersAPI = new UsersHandler();

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

                var response = context.Response;

                response.AddHeader("Access-Control-Allow-Origin", "*");
                response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                response.AddHeader("Access-Control-Allow-Headers", "Content-Type");

                // TODO: comprovar de quin tipus de request es tracta i enviar-ho a un thread a part per a que es gestioni a part
                // El thread s'haùrpa de tancar correctament
                if (request.HttpMethod == "POST")
                {
                    // Aquí pot haber problemes en cas de tenir nulls
                    // no puc simplement fer "return". He de fer el recieve.
                    string? absolutePath = request.Url?.AbsolutePath;

                    // I should control the: segments == null
                    string[]? segments = request.Url?.Segments.Select(s => s.Trim('/')).ToArray().Skip(1).ToArray();

                    if (segments[0] == "users")
                    {
                        _ = handleUserRequests(context, request, response, segments.Skip(1).ToArray());
                    }
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.Created;
                    response.OutputStream.Close();
                }

                Receive();
            }
        }

        private static async Task handleUserRequests(HttpListenerContext? context, HttpListenerRequest? request, HttpListenerResponse response, String[] segments)
        {
            // Això s'ha de controlar de diferent forma. Hem de saber què passa.
            if (request == null) return;
            if (!request.HasEntityBody) return;

            try
            {
                int responseCode = (int)HttpStatusCode.OK;
                if (segments[0] == "signup_user")
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
                    ExitStatus result = await usersAPI.regiterUser(recievedData);
                    reader.Close();
                    body.Close();


                    responseCode = result.code == ExitStatus.Code.OK
                        ? (int)HttpStatusCode.Created
                        : result.code == ExitStatus.Code.BAD_REQUEST
                        ? (int)HttpStatusCode.BadRequest
                        : (int)HttpStatusCode.InternalServerError;
                }
                else if (segments[0] == "signin_user")
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
                    ExitStatus result = await usersAPI.signinUser(recievedData);
                    reader.Close();
                    body.Close();

                    responseCode = result.code == ExitStatus.Code.OK 
                        ? (int)HttpStatusCode.OK
                        : result.code == ExitStatus.Code.BAD_REQUEST
                        ? (int)HttpStatusCode.BadRequest
                        : (int)HttpStatusCode.InternalServerError;
                }
                else
                {
                    responseCode = (int)HttpStatusCode.NotModified;
                }

                response.StatusCode = responseCode;
                response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                response.StatusCode = (int)HttpStatusCode.Conflict;
                response.OutputStream.Close();
            }
        }
    }
}
