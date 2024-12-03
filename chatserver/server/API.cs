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
        private static UsersHandler usersAPI = UsersHandler.Instance;

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
                    if (resources == null)
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="authHeader"></param>
        /// <returns>message to send to user. the "result" is the HTTP.status code</returns>
        private static async Task<ExitStatus> checkAuthHeader(string authHeader)
        {
            if (string.IsNullOrEmpty(authHeader))
            {
                // Si el header no està present, retorna un error
                return new ExitStatus { 
                    status = ExitCodes.UNAUTHORIZED, 
                    message = "Authorization header missing",
                };
            }
            else // té header
            {
                if (authHeader.StartsWith("Bearer "))
                {
                    string token = authHeader.Substring("Bearer ".Length);

                    ExitStatus refreshTokenResult = await SessionHandler.RefreshSession(token, ""); // TODO - complete
                    bool isValid = refreshTokenResult.status == ExitCodes.OK;

                    if (isValid)
                    {
                        return new ExitStatus { 
                            status = ExitCodes.OK, 
                            message = "Valid token", 
                            result = (string)refreshTokenResult.result!,
                        };
                    }
                    else
                    {
                        return new ExitStatus { 
                            status = ExitCodes.UNAUTHORIZED, 
                            message = "Invalid token",
                        };
                    }
                }
                else
                {
                    return new ExitStatus { 
                        status = ExitCodes.BAD_REQUEST, 
                        message = "Invalid authorization format",
                    };
                }
            }
        }

        private static async Task HandleRoute(HttpListenerRequest request, HttpListenerResponse response, List<string> resources)
        {
            try
            {
                ExitStatus sessionResult = await checkAuthorization(request, response);

                if (sessionResult.status == ExitCodes.UNAUTHORIZED)
                {
                    if (resources[0] == "access")
                    {
                        await HandleAccessResource(request, response, resources.Skip(1).ToList());
                    }
                    else if (resources[0] == "refresh_token")
                    {
                        await HandleRefreshToken(request, response);
                    }
                    else
                    {
                        SendJsonResponse(response, JsonSerializer.Serialize(new { status = false }));
                    }
                }
                else if (sessionResult.status == ExitCodes.OK)
                {
                    if (resources.Count == 0)
                    {
                        SendJsonResponse(response, JsonSerializer.Serialize(new { status = true }));
                    }
                    else if (resources[0] == "signout")
                    {
                        await HandleSignOut(request, response);
                    } // else if...
                }
                else
                {
                    _ = NotFoundResponse(response);
                }
            }
            catch (Exception ex)
            {
                // provisional
                return;
            }
        }

        private static async Task<ExitStatus> checkAuthorization(HttpListenerRequest request, HttpListenerResponse response)
        {
            var sessionCookie = request.Cookies["accessToken"];
            var result = new ExitStatus()
            {
                status = ExitCodes.UNAUTHORIZED,
                message = "No access token"
            };

            if (sessionCookie != null)
            {
                // Revisar la validesa de la cookie
                ExitStatus sessionValidation = SessionHandler.CustomValidateToken(sessionCookie.Value);
                result = sessionValidation.status == ExitCodes.OK
                    ? new ExitStatus { status = ExitCodes.OK, message = "Access granted" }
                    : new ExitStatus { status = ExitCodes.UNAUTHORIZED, message = "Invalid session" };

                response.StatusCode = sessionValidation.status == ExitCodes.OK
                    ? (int)HttpStatusCode.OK
                    : (int)HttpStatusCode.Unauthorized;
            }

            return result;

        }

        private static async Task<ExitStatus> HandleRefreshToken(HttpListenerRequest request, HttpListenerResponse response)
        {

            var authHeader = request.Headers["Authorization"];
            ExitStatus authHeaderResult = await checkAuthHeader(authHeader!);
            
            response.StatusCode = authHeaderResult.status == ExitCodes.OK
                ? (int)HttpStatusCode.OK
                : (int)HttpStatusCode.Unauthorized;

            SendJsonResponse(response, JsonSerializer.Serialize(new { 
                status = authHeaderResult.status == ExitCodes.OK, 
                message = (string)authHeaderResult.message!,
                authToken = (string)authHeaderResult.result!
            }));

            return authHeaderResult;
        }

        private static async Task HandleAccessResource(HttpListenerRequest request, HttpListenerResponse response, List<string> resources)
        {
            if (resources.Count > 0)
            {
                var action = resources[0];
                _ = action switch
                {
                    "signin" => await HandleSign(request, response, "signin"),
                    "signup" => await HandleSign(request, response, "signup"),
                    _ => NotFoundResponse(response)
                };
            }
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
                //int sessionId = SessionHandler.GetSessionsCounter();
                TokensStruct tokens = (TokensStruct)(await SessionHandler.GetTokens("")).result!; // TODO - complete
                var ddbb = DDBBHandler.getInstance();
                await ddbb.write("sessions", JsonDocument.Parse($@"{{ ""refreshToken"": ""{tokens.refreshToken}"" }}").RootElement);

                response.Headers.Add("Set-Cookie", $"accessToken={tokens.accessToken}; HttpOnly; Path=/; Expires={DateTime.UtcNow.AddMinutes(60):R};"); // Domain=localhost");
                response.Headers.Add("Set-Cookie", $"userName={(string)result.result!}; HttpOnly; Path=/; Expires={DateTime.UtcNow.AddMinutes(60):R};"); // Domain=localhost");
                response.StatusCode = (int)HttpStatusCode.OK;
                SendJsonResponse(response, JsonSerializer.Serialize(new { status = true, refreshToken = tokens.refreshToken, message = result.message }));
            }
            else
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                SendJsonResponse(response, JsonSerializer.Serialize(new { status = false, error = result.message }));
            }

            return result;
        }

        private static async Task<ExitStatus> HandleSignOut(HttpListenerRequest request, HttpListenerResponse response)
        {
            var sessionCookie = request.Cookies["accessToken"];
            if (sessionCookie == null) return new ExitStatus { status = ExitCodes.BAD_REQUEST };
            
            response.Headers.Add("Set-Cookie", $"accessToken=; HttpOnly; Path=/; Expires={DateTime.UtcNow.AddMinutes(1):R}; Domain=localhost");
            return await SessionHandler.SignOut("");
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
            return new ExitStatus 
            {
                status = ExitCodes.NOT_FOUND, 
                message = "Endpoint not found",
                result = (int)HttpStatusCode.NotFound
            };
        }
    }
}
