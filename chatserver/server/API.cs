using chatserver.server.APIs;
using chatserver.utils;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text;

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

                // CORS headers
                var origin = request.Headers["Origin"];
                if (!string.IsNullOrEmpty(origin))
                {
                    response.AddHeader("Access-Control-Allow-Origin", origin); // Permet l'origen que fa la petició
                }
                response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                response.AddHeader("Access-Control-Allow-Headers", "Content-Type");
                response.AddHeader("Access-Control-Allow-Credentials", "true");

                // TODO: comprovar de quin tipus de request es tracta i enviar-ho a un thread a part per a que es gestioni a part
                // El thread s'haùrpa de tancar correctament
                if (request.HttpMethod == "POST")
                {
                    // TODO
                    // Aquí pot haber problemes en cas de tenir nulls
                    // no puc simplement fer "return". He de fer el recieve.
                    //string? absolutePath = request.Url?.AbsolutePath;

                    var body = request.InputStream;
                    var encoding = request.ContentEncoding;
                    var reader = new StreamReader(body, encoding);

                    // TODO: S'ha de comprovar que ens estan enviant un body. Sinó es respon directament que falta el cos.
                    Logger.RequestServerLogger.Debug("Client data content length: " + request.ContentLength64);

                    string recievedData = reader.ReadToEnd();

                    if (request.ContentLength64 > 0)
                    {
                        // TODO
                        // Hauria de llançar una excepció
                    }

                    reader.Close();
                    body.Close();

                    List<string> requestRoutes = Utils.getUrlRoutes(url: request.Url!);
                    if (requestRoutes[0] == "sign_users")
                    {
                        ExitStatus signResult = await handleSignRequests(recievedData, requestRoutes[1]);

                        int responseCode = signResult.status == ExitCodes.OK
                            ? (int)HttpStatusCode.OK
                            : signResult.status == ExitCodes.BAD_REQUEST
                            ? (int)HttpStatusCode.BadRequest
                            : (int)HttpStatusCode.InternalServerError;

                        var cookie = new Cookie("session-id", "12345")
                        {
                            HttpOnly = true,
                            Secure = true,
                            Expires = DateTime.UtcNow.AddDays(1),
                            Path = "/",
                            Domain = "localhost"
                        };

                        response.Headers.Add("Set-Cookie", cookie.ToString());

                        response.StatusCode = responseCode;
                        response.ContentType = "application/json";
                        var responseObject = new
                        {
                            message = signResult.message,
                            status = "success",
                            data = new { /* altres dades aquí */ }
                        };
                        string jsonResponse = JsonSerializer.Serialize(responseObject);
                        byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
                        response.ContentLength64 = buffer.Length;
                        response.OutputStream.Write(buffer, 0, buffer.Length);
                    }
                }
                else if (request.HttpMethod == "GET")
                {
                    bool userStatus = false;

                    if (request.Cookies.Count > 0)
                    {
                        userStatus = request.Cookies[0].Value == "12345" ? true : false;
                    }

                    response.ContentType = "application/json";
                    var responseObject = new
                    {
                        status = userStatus,
                    };
                    string jsonResponse = JsonSerializer.Serialize(responseObject);
                    byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);

                }
                else if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = (int)HttpStatusCode.OK;
                }

                // Enviem resposta
                response.OutputStream.Close();

                Receive();
            }
        }

        private static async Task<ExitStatus> handleSignRequests(string recievedData, string action)
        {
            int responseCode = (int)HttpStatusCode.OK;
            string responseMessage = "";
            ExitStatus exitStatus = new ExitStatus();
            try
            {
                ExitStatus result;

                if (action == "signup_user")
                {
                    result = await usersAPI.signUpUser(recievedData);
                }
                else if (action == "signin_user")
                {
                    result = await usersAPI.signInUser(recievedData);
                }
                else
                {
                    throw new Exception(CustomExceptionCdes.BAD_REQUESTS.ToString());
                }

                exitStatus.status = result.status;
                exitStatus.message = result.message;
                exitStatus.result = result.result;
            }
            catch (Exception ex)
            {
                responseCode = (int)HttpStatusCode.Conflict;
            }

            return exitStatus;
        }
    }
}
