using chatserver.server.APIs;
using chatserver.utils;
using System.Net;
using System.Text.Json;
using System.Text;
using chatserver.authentication;
using chatserver.DDBB;

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
            _listener.Prefixes.Add($"http://*:{Port}/"); // Canvi a interpolació
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
            if (!_listener.IsListening) return;

            var context = _listener.EndGetContext(result);
            var request = context.Request;
            var response = context.Response;

            // Afegim les capçaleres CORS
            addCorsHeaders(response, request);

            try
            {
                if (request.HttpMethod == "POST")
                {
                    await handlePostRequest(request, response);
                }
                else if (request.HttpMethod == "GET")
                {
                    await handleGetRequest(request, response);
                }
                else if (request.HttpMethod == "PUT")
                {
                    await handlePutRequest(request, response);
                }
                else if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = (int)HttpStatusCode.OK;
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                }
            }
            catch (Exception ex)
            {
                // Afegim informació d'error a la resposta
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                byte[] errorBytes = Encoding.UTF8.GetBytes($"Error: {ex.Message}");
                response.OutputStream.Write(errorBytes, 0, errorBytes.Length);
            }
            finally
            {
                response.OutputStream.Close(); // Tanca el flux de la resposta
                Receive(); // Continua escoltant noves peticions
            }
        }

        private static async Task handlePutRequest(HttpListenerRequest request, HttpListenerResponse response)
        {
            List<string> requestRoutes = Utils.getUrlRoutes(url: request.Url!);
            if (requestRoutes.Count <= 0)
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            if (requestRoutes[0] == "signout")
            {
                var sessionCookie = request.Cookies["session-id"];
                if (sessionCookie != null)
                {
                    ExitStatus result = await SessionHandler.signOutHandler(sessionCookie.Value);
                }
            }
        }

        private static async Task handleGetRequest(HttpListenerRequest request, HttpListenerResponse response)
        {

            List<string> requestRoutes = Utils.getUrlRoutes(url: request.Url!);

            if (requestRoutes.Count <= 0)
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }
            
            if (requestRoutes[0] == "im_i_logged_in")
            {
                Logger.ConsoleLogger.Debug("A user has requested access");
                Logger.ApiLogger.Debug("A user has requested access");
                bool userStatus = false;

                var sessionCookie = request.Cookies["session-id"];
                if (sessionCookie != null)
                {
                    ExitStatus result = await SessionHandler.isSessionActive(sessionCookie.Value);
                    if (result.status == ExitCodes.OK) userStatus = (bool)result.result;
                }   

                response.StatusCode = (int)HttpStatusCode.OK;
                var responseObject = new
                {
                    status = userStatus,
                };
                string jsonResponse = JsonSerializer.Serialize(responseObject);

                sendJsonResponse(response, jsonResponse);
            }
        }

        private static async Task handlePostRequest(HttpListenerRequest request, HttpListenerResponse response)
        {
            // Comprovem que hi hagi un cos a la petició
            if (request.ContentLength64 == 0)
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                var errorResponse = new { error = "Request body is missing" };
                sendJsonResponse(response, JsonSerializer.Serialize(errorResponse));
                return;
            }

            // Llegim el cos de la petició
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            string recievedData = reader.ReadToEnd();

            List<string> requestRoutes = Utils.getUrlRoutes(url: request.Url!);
            if (requestRoutes.Count == 0)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                var errorResponse = new { error = "No route specified" };
                sendJsonResponse(response, JsonSerializer.Serialize(errorResponse));
                return;
            }

            if (requestRoutes[0] == "sign_users")
            {
                ExitStatus signResult = await handleSignRequests(recievedData, requestRoutes[1]);

                int responseCode = signResult.status switch
                {
                    ExitCodes.OK => (int)HttpStatusCode.OK,
                    ExitCodes.BAD_REQUEST => (int)HttpStatusCode.BadRequest,
                    _ => (int)HttpStatusCode.InternalServerError
                };

                int sessionId = SessionHandler.getSessionsCounter();
                DDBBHandler dDBBHandler = DDBBHandler.getInstance();
                await dDBBHandler.write("sessions", JsonDocument.Parse($@"{{ ""sessionId"": ""{sessionId}"" }}").RootElement);

                var cookieValue = $"session-id={sessionId}; HttpOnly; Path=/; Expires={DateTime.UtcNow.AddDays(1):R}; Secure={false}; Domain=localhost";
                response.Headers.Add("Set-Cookie", cookieValue);

                response.StatusCode = responseCode;
                var responseObject = new
                {
                    message = signResult.message,
                    status = "success",
                    data = signResult.result
                };
                sendJsonResponse(response, JsonSerializer.Serialize(responseObject));
            }
        }

        private static void sendJsonResponse(HttpListenerResponse response, string jsonData)
        {
            response.ContentType = "application/json";
            byte[] buffer = Encoding.UTF8.GetBytes(jsonData);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        private static async Task<ExitStatus> handleSignRequests(string recievedData, string action)
        {
            ExitStatus exitStatus = new ExitStatus();
            try
            {
                ExitStatus result = action switch
                {
                    "signUp" => await usersAPI.signUpUser(recievedData),
                    "signIn" => await usersAPI.signInUser(recievedData),
                    _ => throw new ArgumentException("Invalid action")
                };

                exitStatus.status = result.status;
                exitStatus.message = result.message;
                exitStatus.result = result.result;
            }
            catch (Exception ex)
            {
                exitStatus.status = ExitCodes.ERROR;
                exitStatus.message = ex.Message;
            }

            return exitStatus;
        }

        private static void addCorsHeaders(HttpListenerResponse response, HttpListenerRequest request)
        {
            var origin = request.Headers["Origin"];
            if (!string.IsNullOrEmpty(origin))
            {
                response.AddHeader("Access-Control-Allow-Origin", origin);
            }
            response.AddHeader("Access-Control-Allow-Methods", "GET, POST, PUT, OPTIONS");
            response.AddHeader("Access-Control-Allow-Headers", "Content-Type");
            response.AddHeader("Access-Control-Allow-Credentials", "true");
        }
    }
}
