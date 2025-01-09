using chatserver.server.APIs;
using chatserver.utils;
using System.Net;
using System.Text.Json;
using System.Text;
using chatserver.authentication;
using chatserver.DDBB;
using log4net.Util;
using System.Reflection.Metadata;

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

        private static async Task HandleRoute(HttpListenerRequest request, HttpListenerResponse response, List<string> resources)
        {
            try
            {
                Logger.ConsoleLogger.Debug("[HandleRoute] - Start");
                ExitStatus sessionResult = checkAuthorization(request, response);

                if (sessionResult.status == ExitCodes.UNAUTHORIZED)
                {
                    if (resources.Count == 0)
                    {
                        await HandleRefreshToken(request, response);
                    }
                    else if (resources[0] == "access")
                    {
                        await HandleAccessResource(request, response, resources.Skip(1).ToList());
                    }
                    else
                    {
                        // TODO
                        // S'han d'enviar response status?
                        SendJsonResponse(response, JsonSerializer.Serialize(new { status = false }));
                    }
                }
                else if (sessionResult.status == ExitCodes.OK)
                {
                    if (resources.Count == 0)
                    {
                        SendJsonResponse(response, JsonSerializer.Serialize(new { status = true }));
                    }
                    else if (resources[0] == "user")
                    {
                        await HandleUserRequests(request, response, resources.Skip(1).ToList());
                    }
                    else if (resources[0] == "signout")
                    {
                        await HandleSignOut(request, response);
                    }
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

        private static ExitStatus checkAuthorization(HttpListenerRequest request, HttpListenerResponse response)
        {
            Logger.ConsoleLogger.Debug("[checkAuthorization] - Start");
            var sessionCookie = request.Cookies["accessToken"];
            // TODO
            // Control valid
            var result = new ExitStatus()
            {
                status = ExitCodes.UNAUTHORIZED,
                message = "No access token"
            };

            if (sessionCookie != null)
            {
                Logger.ConsoleLogger.Debug("[checkAuthorization] - There is cookie");
                // Revisar la validesa de la cookie
                ExitStatus sessionValidation = SessionHandler.CustomValidateToken(sessionCookie.Value);
                result = sessionValidation.status == ExitCodes.OK
                    ? new ExitStatus { status = ExitCodes.OK, message = "Access granted" }
                    : new ExitStatus { status = ExitCodes.UNAUTHORIZED, message = "Invalid session" };

                response.StatusCode = sessionValidation.status == ExitCodes.OK
                    ? (int)HttpStatusCode.OK
                    : (int)HttpStatusCode.Unauthorized;
            }
            Logger.ConsoleLogger.Debug("[checkAuthorization] - End");
            return result;

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="authHeader"></param>
        /// <returns>message to send to user. the "result" is the HTTP.status code</returns>
        private static async Task<ExitStatus> CheckAuthHeader(string authHeader, string username)
        {
            Logger.ConsoleLogger.Debug("[checkAuthHeader] - Start");
            if (string.IsNullOrEmpty(authHeader))
            {
                // Si el header no està present, retorna un error
                return new ExitStatus
                {
                    status = ExitCodes.UNAUTHORIZED,
                    message = "Authorization header missing",
                };
            }
            else // té header
            {
                if (authHeader.StartsWith("Bearer "))
                {
                    string token = authHeader.Substring("Bearer ".Length);

                    ExitStatus refreshTokenResult = await SessionHandler.RefreshSession(token, username);
                    bool isValid = refreshTokenResult.status == ExitCodes.OK;

                    if (isValid)
                    {
                        Logger.ConsoleLogger.Debug("[checkAuthHeader] - End - Ok");
                        Logger.ConsoleLogger.Debug($"[checkAuthHeader] - result: {(string)refreshTokenResult.result!}");
                        return new ExitStatus
                        {
                            status = ExitCodes.OK,
                            message = "Valid token",
                            result = (string)refreshTokenResult.result!,
                        };
                    }
                    else
                    {
                        Logger.ConsoleLogger.Debug("[checkAuthHeader] - End - unauthorized");
                        return new ExitStatus
                        {
                            status = ExitCodes.UNAUTHORIZED,
                            message = "Invalid token",
                        };
                    }
                }
                else
                {
                    Logger.ConsoleLogger.Debug("[checkAuthHeader] - End - Bad request");
                    return new ExitStatus
                    {
                        status = ExitCodes.BAD_REQUEST,
                        message = "Invalid authorization format",
                    };
                }
            }
        }

        private static async Task<ExitStatus> HandleRefreshToken(HttpListenerRequest request, HttpListenerResponse response)
        {
            Logger.ConsoleLogger.Debug("[HandleRefreshToken] - Start");
            var authHeader = request.Headers["Authorization"];


            // TODO: El refresh token aquí no existeix. Ha caducat. He de trobar un altre forma de trobar el username.
            var sessionCookie = request.Cookies["accessToken"];
            if (sessionCookie == null) return new ExitStatus { status = ExitCodes.BAD_REQUEST };
            ExitStatus usernameResult = await SessionHandler.GetUsernameFromAccessToken(sessionCookie.Value);
            if (usernameResult.status != ExitCodes.OK) return new ExitStatus { status = ExitCodes.BAD_REQUEST, message="No session stored with this username" };
            string username = (string)usernameResult.result!;

            ExitStatus authHeaderResult = await CheckAuthHeader(authHeader!, username);

            string accessToken = (string)authHeaderResult.result!;

            if (authHeaderResult.status == ExitCodes.OK)
            {
                response.StatusCode = (int)HttpStatusCode.OK;
                AddCookie(response, "accessToken", accessToken, 15);
            }
            else
            {
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
            }

            SendJsonResponse(response, JsonSerializer.Serialize(new { 
                status = authHeaderResult.status == ExitCodes.OK, 
                message = (string)authHeaderResult.message!,
                authToken = accessToken
            }));

            Logger.ConsoleLogger.Debug("[HandleRefreshToken] - End");
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
                "signin" => await usersAPI.SignInUser((string)bodyResult.result!),
                "signup" => await usersAPI.signUpUser((string)bodyResult.result!),
                _ => throw new ArgumentException("Invalid action")
            };

            if (result.status == ExitCodes.OK)
            {
                //int sessionId = SessionHandler.GetSessionsCounter();
                var ddbb = DDBBHandler.Instance;
                string username = (string)result.result!;
                await ddbb.delete("sessions", "username", username);
                TokensStruct tokens = (TokensStruct)(await SessionHandler.GetTokens(username)).result!; // TODO - complete

                AddCookie(response, "accessToken", tokens.accessToken, 15);
                AddCookie(response, "username", username, 0, false);
                response.StatusCode = (int)HttpStatusCode.OK;
                SendJsonResponse(response, JsonSerializer.Serialize(new { status = true, refreshToken = tokens.refreshToken, message = result.message }));
            }
            else
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                SendJsonResponse(response, JsonSerializer.Serialize(new { status = false, message = result.message }));
            }

            return result;
        }

        private static async Task<ExitStatus> HandleSignOut(HttpListenerRequest request, HttpListenerResponse response)
        {
            var sessionCookie = request.Cookies["accessToken"];
            if (sessionCookie == null) return new ExitStatus { status = ExitCodes.BAD_REQUEST };
            ExitStatus usernameResult = await SessionHandler.GetUsernameFromAccessToken(sessionCookie.Value);
            if (usernameResult.status != ExitCodes.OK) return new ExitStatus { status = ExitCodes.BAD_REQUEST, message = "No session stored with this username" };
            string username = (string)usernameResult.result!;

            _ = await SessionHandler.deleteSession(username);
            AddCookie(response, "accessToken", "", 1);

            return await SessionHandler.SignOut("");
        }

        private static async Task<ExitStatus> HandleUserRequests(HttpListenerRequest request, HttpListenerResponse response, List<string> resources)
        {
            try
            {
                if (resources[0] == null)
                {
                    return new ExitStatus()
                    {
                        status = ExitCodes.BAD_REQUEST,
                        message = "HTTP /user need more options"
                    };
                }

                var sessionCookie = request.Cookies["accessToken"];
                var accessToken = sessionCookie!.Value;
                ExitStatus usernameResult = await SessionHandler.GetUsernameFromAccessToken(accessToken);
                if (usernameResult.status == ExitCodes.ERROR)
                {
                    throw new Exception(usernameResult.message);
                }
                string username = (string)usernameResult.result!;
                ExitStatus userReuslt = new ExitStatus()
                {
                    message = ""
                };
                if (resources[0] == "contacts")
                {
                    if (request.HttpMethod == "GET")
                    {
                        UsersHandler users = UsersHandler.Instance;
                        userReuslt = await users.RetrieveContacts(username);
                    }
                    else if (request.HttpMethod == "PATCH")
                    {
                        // add contact
                        // retrieve contact username from body
                        var bodyResult = CheckForBody(request, response);
                        if (bodyResult.status != ExitCodes.OK) return bodyResult;

                        string data = (string)bodyResult.result!;
                        JsonDocument parsedData = JsonDocument.Parse(data);
                        var root = parsedData.RootElement;

                        string? contactUsername = root.GetProperty("contactUsername").GetString();

                        UsersHandler users = UsersHandler.Instance;
                        userReuslt = await users.AddContactOrGroup(username, contactUsername!);
                    }
                    else
                    {

                    }
                    // TODO : gestionar quan es retorna un error
                    response.StatusCode = usernameResult.status == ExitCodes.OK ? (int)HttpStatusCode.OK : (int)HttpStatusCode.InternalServerError;
                    SendJsonResponse(response, JsonSerializer.Serialize(new
                    {
                        status = true,
                        message = userReuslt.message,
                        contacts = usernameResult.status == ExitCodes.OK ? userReuslt.result : null,
                    }));
                }

                return new ExitStatus();
            }
            catch (Exception ex)
            {
                return new ExitStatus()
                {
                    status = ExitCodes.ERROR,
                    message = ex.Message
                };
            }
        }

        private static ExitStatus GetValueFromCookie(HttpListenerRequest request, string cookieName)
        {
            var username = request.Cookies[cookieName];
            if (username == null) return new ExitStatus { status = ExitCodes.NOT_FOUND };
            return new ExitStatus()
            {
                result = username.Value
            };
        }

        private static ExitStatus CheckForBody(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (request.ContentLength64 == 0)
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
            response.AddHeader("Access-Control-Allow-Methods", "GET, POST, PUT, PATCH, OPTIONS");
            response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Authorization");
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

        private static void AddCookie(HttpListenerResponse response, string key, string value, int liveMinutes, bool httpOnly = true)
        {
            Logger.ConsoleLogger.Debug("[AddCookie] - Start");
            Logger.ConsoleLogger.Debug($"[AddCookie] - Now: {DateTime.UtcNow}");
            Logger.ConsoleLogger.Debug($"[AddCookie] - Expires: {DateTime.UtcNow.AddMinutes(liveMinutes)}");
            string expires = liveMinutes > 0 ? $"Expires={DateTime.UtcNow.AddMinutes(liveMinutes):R}" : "";
            string httponly = httpOnly ? "HttpOnly" : "";
            response.Headers.Add("Set-Cookie", $"{key}={value}; {httponly}; Path=/; {expires};");
            Logger.ConsoleLogger.Debug("[AddCookie] - End");
        }
    }
}
