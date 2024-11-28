using chatserver.server.APIs;
using chatserver.utils;
using System.Net;
using System.Text.Json;
using System.Text;
using chatserver.authentication;
using chatserver.DDBB;
using MongoDB.Driver;
using DnsClient;
using System.Runtime.Versioning;

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
                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = (int)HttpStatusCode.OK;
                    return;
                }

                List<string> resources = Utils.getUrlRoutes(url: request.Url!);

                if (resources != null)
                {
                    // We should evaluate the return
                    _ = resources[0] switch
                    {
                        "access" => await handleAccess(request, response, resources),
                        _ => notFoundResponse(response)
                    };
                }

                // deprecated
                /*
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
                 */
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

        private static ExitStatus notFoundResponse(HttpListenerResponse response)
        {
            response.StatusCode = (int)HttpStatusCode.NotFound;
            return new ExitStatus();
        }

        private static async Task<ExitStatus> handleAccess(HttpListenerRequest request, HttpListenerResponse response, List<string> resources)
        {
            ExitStatus result = new ExitStatus();
            if (resources.Count >= 2)
            { 
                string action = resources[1];
                result = action switch
                {
                    "signin" => await handleSignin(request, response),
                    "signup" => await handleSignup(request, response),
                    "logout" => await handleSignout(request),
                    _ => new ExitStatus() // provisional
                };
            }

            // TODO
            // S'ha de gestionar el cas en el qual es demana per accés "GET /access"

            return result; // provisional
        }

        private static ExitStatus checkForBody(HttpListenerRequest request, HttpListenerResponse response)
        {
            if(request.HttpMethod != "POST" && request.HttpMethod != "PUT" || request.ContentLength64 == 0)
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                var errorResponse = new { error = "Request body is missing" };
                sendJsonResponse(response, JsonSerializer.Serialize(errorResponse));
                return new ExitStatus()
                {
                    status = ExitCodes.ERROR,
                    message = "Should have body and should be POST or PUT."
                };
            }
            else
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                return new ExitStatus()
                {
                    status = ExitCodes.OK,
                    result = reader.ReadToEnd()
                };
            }
        }
        
        private static async Task<ExitStatus> handleSignin(HttpListenerRequest request, HttpListenerResponse response)
        {
            ExitStatus result = checkForBody(request, response);
            if (result.status == ExitCodes.OK)
            {
                result = await usersAPI.signInUser((string)result.result!);
                if (result.status != ExitCodes.OK)
                {
                    throw new Exception(result.message);
                }
                // prepara cookies amb sessió
                //      guarda la cookie a la bbdd
                int sessionId = SessionHandler.getSessionsCounter();
                DDBBHandler dDBBHandler = DDBBHandler.getInstance();
                await dDBBHandler.write("sessions", JsonDocument.Parse($@"{{ ""sessionId"": ""{sessionId}"" }}").RootElement);
                //      configura la cookie
                var cookieValue = $"session-id={sessionId}; HttpOnly; Path=/; Expires={DateTime.UtcNow.AddDays(1):R}; Secure={false}; Domain=localhost";
                response.Headers.Add("Set-Cookie", cookieValue);

                int responseCode = result.status switch
                {
                    ExitCodes.OK => (int)HttpStatusCode.OK,
                    ExitCodes.BAD_REQUEST => (int)HttpStatusCode.BadRequest,
                    _ => (int)HttpStatusCode.InternalServerError
                };

                // prepara resposta
                response.StatusCode = responseCode;
                var responseObject = new
                {
                    message = result.message,
                    status = "success",
                    data = result.result
                };
                sendJsonResponse(response, JsonSerializer.Serialize(responseObject));
            }
            else
            {
                Logger.ApiLogger.Error(result.message);
            }

            return result; // provisional
        }

        private static async Task<ExitStatus> handleSignup(HttpListenerRequest request, HttpListenerResponse response)
        {
            return new ExitStatus(); // provisional
        }

        private static async Task<ExitStatus> handleSignout(HttpListenerRequest request)
        {
            var sessionCookie = request.Cookies["session-id"];
            ExitStatus result = new ExitStatus();
            if (sessionCookie != null)
            {
                result = await SessionHandler.signOutHandler(sessionCookie.Value);
            }
            return result;
        }


        private static void sendJsonResponse(HttpListenerResponse response, string jsonData)
        {
            response.ContentType = "application/json";
            byte[] buffer = Encoding.UTF8.GetBytes(jsonData);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        // deprecated
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
            response.AddHeader("Access-Control-Allow-Methods", "GET, POST, PUT, PATCH, OPTIONS");
            response.AddHeader("Access-Control-Allow-Headers", "Content-Type");
            response.AddHeader("Access-Control-Allow-Credentials", "true");
        }
        
        // deprecated
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

        // deprecated
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

        // deprecated
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

                // prepara cookies amb sessió
                //      guarda la cookie a la bbdd
                int sessionId = SessionHandler.getSessionsCounter();
                DDBBHandler dDBBHandler = DDBBHandler.getInstance();
                await dDBBHandler.write("sessions", JsonDocument.Parse($@"{{ ""sessionId"": ""{sessionId}"" }}").RootElement);
                //      configura la cookie
                var cookieValue = $"session-id={sessionId}; HttpOnly; Path=/; Expires={DateTime.UtcNow.AddDays(1):R}; Secure={false}; Domain=localhost";
                response.Headers.Add("Set-Cookie", cookieValue);

                // prepara resposta
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
    }
}
