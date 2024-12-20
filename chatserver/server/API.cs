﻿using chatserver.server.APIs;
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
            ExitStatus usernameResult = GetValueFromCookie(request, "username");
            if (usernameResult.status != ExitCodes.OK)
            {
                Logger.ConsoleLogger.Debug("[HandleRefreshToken] - No username");
                // TODO
                // Haurà de retornar un error conforme està unauthorized.
                // necessitem el username. Si no està, l'haurem de recuperar mitjançant un inici de sessió
            }
            string username = usernameResult.status == ExitCodes.OK ? (string)usernameResult.result! : "";

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
                TokensStruct tokens = (TokensStruct)(await SessionHandler.GetTokens("")).result!; // TODO - complete
                var ddbb = DDBBHandler.Instance;
                string username = (string)result.result!;

                // TODO
                // Crec que la part de guardar això a la bbdd no hauria de ser aquí
                // delete last refresh token (if exists)
                await ddbb.delete("sessions", "username", username);

                // add new refresh token
                var jsonDocument = new
                {
                    username = username,
                    refreshToken = tokens.refreshToken
                };
                var jsonElement = JsonDocument.Parse(JsonSerializer.Serialize(jsonDocument)).RootElement;
                await ddbb.write("sessions", jsonElement);

                AddCookie(response, "accessToken", tokens.accessToken, 15);
                AddCookie(response, "userName", username, 0);
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

            var username = request.Cookies["username"];
            if (username == null) return new ExitStatus { status = ExitCodes.BAD_REQUEST };

            _ = await SessionHandler.deleteSession(username.Value);
            AddCookie(response, "accessToken", "", 1);

            return await SessionHandler.SignOut("");
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
