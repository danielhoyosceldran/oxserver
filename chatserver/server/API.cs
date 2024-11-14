using chatserver.server.APIs;
using chatserver.utils;
using log4net.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using chatserver.utils;

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

                    if (segments[0] == "sign_users")
                    {
                        // S'ha de comprovar que els paràmetres no són null
                        ExitStatus signResult = await handleSignRequests(context, request, response, segments.Skip(1).ToArray());
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

        private static async Task<ExitStatus> handleSignRequests(HttpListenerContext context, HttpListenerRequest request, HttpListenerResponse response, String[] segments)
        {
            int responseCode = (int)HttpStatusCode.OK;
            ExitStatus exitStatus = new ExitStatus();
            try
            {
                var body = request.InputStream;
                var encoding = request.ContentEncoding;
                var reader = new StreamReader(body, encoding);

                if (request.ContentType != null)
                    Logger.RequestServerLogger.Debug("Client data content type: " + request.ContentType);
                Logger.RequestServerLogger.Debug("Client data content length: " + request.ContentLength64);

                // Reading data
                string recievedData = reader.ReadToEnd();
                ExitStatus result;

                string rute = segments[0];
                if (rute == "signup_user")
                {
                    result = await usersAPI.signUpUser(recievedData);
                }
                else if (rute == "signin_user")
                {
                    result = await usersAPI.signInUser(recievedData);
                }
                else
                {
                    throw new Exception(CustomExceptionCdes.BAD_REQUESTS.ToString());
                }
                
                reader.Close();
                body.Close();

                exitStatus.status = result.status;
                exitStatus.message = result.message;
                exitStatus.result = result;

                responseCode = result.status == ExitCodes.OK
                    ? (int)HttpStatusCode.OK
                    : result.status == ExitCodes.BAD_REQUEST
                    ? (int)HttpStatusCode.BadRequest
                    : (int)HttpStatusCode.InternalServerError;
            }
            catch (Exception ex)
            {
                responseCode = (int)HttpStatusCode.Conflict;
            }
            finally
            {
                response.StatusCode = responseCode;
                response.OutputStream.Close();
            }
                return exitStatus;
        }
    }
}
