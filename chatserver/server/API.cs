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

        private static HttpListener _listener = new HttpListener();
        private static UsersHandler usersAPI = new UsersHandler();

        public static async Task Start()
        {
            _listener.Prefixes.Add($"http://*:{Port}/");
            _listener.Start();
            Listen();
        }

        public static void Stop() => _listener.Stop();

        private static void Listen()
        {
            _listener.BeginGetContext(new AsyncCallback(HandleRequest), _listener);
        }

        private async static void HandleRequest(IAsyncResult result)
        {
            if (!_listener.IsListening) return;

            var context = _listener.EndGetContext(result);
            var request = context.Request;
            var response = context.Response;

            try
            {
                AddCorsHeaders(response, request);

                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = (int)HttpStatusCode.OK;
                }
                else
                {
                    var resources = Utils.GetUrlRoutes(request.Url!);
                    if (resources == null || resources.Count == 0)
                    {
                        NotFoundResponse(response);
                    }
                    else
                    {
                        await HandleRoute(request, response, resources);
                    }
                }
            }
            catch (Exception ex)
            {
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                SendJsonResponse(response, JsonSerializer.Serialize(new { error = ex.Message }));
            }
            finally
            {
                response.OutputStream.Close();
                Listen();
            }
        }

        private static async Task<ExitStatus> checkAuthHeader(string authHeader)
        {
            if (string.IsNullOrEmpty(authHeader))
            {
                // Si el header no està present, retorna un error
                return new ExitStatus { 
                    status = ExitCodes.UNAUTHORIZED, 
                    message = "Authorization header missing", 
                    result = (int)HttpStatusCode.Unauthorized 
                };
            }
            else // té header
            {
                // Processa el token (p. ex., Bearer token)
                if (authHeader.StartsWith("Bearer "))
                {
                    string token = authHeader.Substring("Bearer ".Length);

                    ExitStatus isActive = await SessionHandler.IsSessionActiveJWT("");
                    bool isValid = (bool)isActive.result!;

                    if (isValid)
                    {
                        return new ExitStatus { 
                            status = ExitCodes.OK, 
                            message = "Valid token", 
                            result = (int)HttpStatusCode.OK 
                        };
                    }
                    else
                    {
                        return new ExitStatus { 
                            status = ExitCodes.UNAUTHORIZED, 
                            message = "Invalid token", 
                            result = (int)HttpStatusCode.Unauthorized 
                        };
                    }
                }
                else
                {
                    // Si el header no comença amb "Bearer ", retorna un error
                    return new ExitStatus { 
                        status = ExitCodes.BAD_REQUEST, 
                        message = "Invalid authorization format", 
                        result = (int)HttpStatusCode.BadRequest 
                    };
                }
            }
        }

        private static async Task<ExitStatus> HandleSession(HttpListenerRequest request, HttpListenerResponse response)
        {
            // 1. check if the session is active
            var sessionCookie = request.Cookies["session-id"];
            var result = new ExitStatus();

            if (sessionCookie == null)
            {
                var authHeader = request.Headers["Authorization"];
                ExitStatus authHeaderResult = await checkAuthHeader(authHeader);
                if (authHeaderResult.status != ExitCodes.OK)
                {
                    response.StatusCode = (int)authHeaderResult.result!;
                    SendJsonResponse(response, JsonSerializer.Serialize(new { message = (string)authHeaderResult.message! }));
                }
                
            }
            else
            {
                var sessionValidation = await SessionHandler.IsSessionActive(sessionCookie.Value);
                result = sessionValidation.status == ExitCodes.OK
                    ? new ExitStatus { status = ExitCodes.OK, message = "Access granted" }
                    : new ExitStatus { status = ExitCodes.UNAUTHORIZED, message = "Invalid session" };

                response.StatusCode = sessionValidation.status == ExitCodes.OK
                    ? (int)HttpStatusCode.OK
                    : (int)HttpStatusCode.Unauthorized;
            }

            SendJsonResponse(response, JsonSerializer.Serialize(new { status = result.status == ExitCodes.OK, message = result.message }));

            return new ExitStatus();

        }

        private static ExitStatus GetSessionTokens(HttpListenerRequest request)
        {
            return new ExitStatus(); // provisional
        }

        private static async Task HandleRoute(HttpListenerRequest request, HttpListenerResponse response, List<string> resources)
        {

            var result = resources[0] switch
            {
                "access" => await HandleAccess(request, response, resources.Skip(1).ToList()),
                _ => NotFoundResponse(response)
            };
        }

        private static async Task<ExitStatus> HandleAccess(HttpListenerRequest request, HttpListenerResponse response, List<string> resources)
        {
            if (resources.Count == 0)
            {
                return await HandleAccessStatus(request, response);
            }

            var action = resources[0];
            return action switch
            {
                "signin" => await HandleSign(request, response, "signin"),
                "signup" => await HandleSign(request, response, "signup"),
                "signout" => await HandleSignOut(request),
                _ => NotFoundResponse(response)
            };
        }

        private static async Task<ExitStatus> HandleAccessStatus(HttpListenerRequest request, HttpListenerResponse response)
        {
            // TODO
            // Adabtar per a JWT
            var sessionCookie = request.Cookies["session-id"];
            var result = new ExitStatus();

            if (sessionCookie == null)
            {
                result = new ExitStatus { status = ExitCodes.UNAUTHORIZED, message = "No session found" };
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
            }
            else
            {
                var sessionValidation = await SessionHandler.IsSessionActive(sessionCookie.Value);
                result = sessionValidation.status == ExitCodes.OK
                    ? new ExitStatus { status = ExitCodes.OK, message = "Access granted" }
                    : new ExitStatus { status = ExitCodes.UNAUTHORIZED, message = "Invalid session" };

                response.StatusCode = sessionValidation.status == ExitCodes.OK
                    ? (int)HttpStatusCode.OK
                    : (int)HttpStatusCode.Unauthorized;
            }

            SendJsonResponse(response, JsonSerializer.Serialize(new { status = result.status == ExitCodes.OK, message = result.message }));
            return result;
        }



        private static async Task<ExitStatus> HandleSign(HttpListenerRequest request, HttpListenerResponse response, string action)
        {
            var bodyResult = CheckForBody(request, response);
            if (bodyResult.status != ExitCodes.OK) return bodyResult;

            var result = action switch
            {
                "signin" => await usersAPI.signInUser((string)bodyResult.result!),
                "signup" => await usersAPI.signUpUser((string)bodyResult.result!),
                _ => throw new ArgumentException("Invalid action")
            };

            if (result.status == ExitCodes.OK)
            {
                int sessionId = SessionHandler.GetSessionsCounter();
                var ddbb = DDBBHandler.getInstance();
                await ddbb.write("sessions", JsonDocument.Parse($@"{{ ""sessionId"": ""{sessionId}"" }}").RootElement);

                response.Headers.Add("Set-Cookie", $"session-id={sessionId}; HttpOnly; Path=/; Expires={DateTime.UtcNow.AddDays(1):R}; Domain=localhost");
                response.StatusCode = (int)HttpStatusCode.OK;
                SendJsonResponse(response, JsonSerializer.Serialize(new { message = result.message, status = "success" }));
            }
            else
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                SendJsonResponse(response, JsonSerializer.Serialize(new { error = result.message }));
            }

            return result;
        }

        private static async Task<ExitStatus> HandleSignOut(HttpListenerRequest request)
        {
            var sessionCookie = request.Cookies["session-id"];
            if (sessionCookie == null) return new ExitStatus { status = ExitCodes.BAD_REQUEST };

            return await SessionHandler.SignOut(sessionCookie.Value);
        }

        private static ExitStatus CheckForBody(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (request.HttpMethod != "POST" || request.ContentLength64 == 0)
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                SendJsonResponse(response, JsonSerializer.Serialize(new { error = "Missing body" }));
                return new ExitStatus { status = ExitCodes.BAD_REQUEST, message = "Missing body" };
            }

            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            return new ExitStatus { status = ExitCodes.OK, result = reader.ReadToEnd() };
        }

        private static void SendJsonResponse(HttpListenerResponse response, string jsonData)
        {
            response.ContentType = "application/json";
            byte[] buffer = Encoding.UTF8.GetBytes(jsonData);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        private static void AddCorsHeaders(HttpListenerResponse response, HttpListenerRequest request)
        {
            response.AddHeader("Access-Control-Allow-Origin", request.Headers["Origin"] ?? "*");
            response.AddHeader("Access-Control-Allow-Methods", "GET, POST, PUT, OPTIONS");
            response.AddHeader("Access-Control-Allow-Headers", "Content-Type");
            response.AddHeader("Access-Control-Allow-Credentials", "true");
        }

        private static ExitStatus NotFoundResponse(HttpListenerResponse response)
        {
            response.StatusCode = (int)HttpStatusCode.NotFound;
            SendJsonResponse(response, JsonSerializer.Serialize(new { error = "Endpoint not found" }));
            return new ExitStatus { status = ExitCodes.NOT_FOUND, message = "Endpoint not found" };
        }
    }
}
