﻿using chatserver.server.APIs;
using chatserver.utils;
using log4net.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using chatserver.utils;
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

                    var body = request.InputStream;
                    var encoding = request.ContentEncoding;
                    var reader = new StreamReader(body, encoding);

                    // TODO: S'ha de comprovar que ens estan enviant un body. Sinó es respon directament que falta el cos.
                    Logger.RequestServerLogger.Debug("Client data content length: " + request.ContentLength64);

                    string recievedData = reader.ReadToEnd();

                    reader.Close();
                    body.Close();

                    List<string> requestRoutes = Utils.getUrlRoutes(url: request.Url!);
                    if (requestRoutes[0] == "sign_users")
                    {
                        ExitStatus signResult = await handleSignRequests(recievedData, response, requestRoutes[1]);
                    }
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    response.OutputStream.Close();
                }

                Receive();
            }
        }

        private static async Task<ExitStatus> handleSignRequests(string recievedData, HttpListenerResponse response, string action)
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
                // TODO. S'ha de configurar bé la resposta
                // La resposta s'envia al retornar? Crec que ho hauria de fer així.
                response.StatusCode = responseCode;
                response.ContentType = "application/json";
                var responseObject = new
                {
                    message = responseMessage,
                    status = "success",
                    data = new { /* altres dades aquí */ }
                };
                string jsonResponse = JsonSerializer.Serialize(responseObject);
                byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
                return exitStatus;
        }
    }
}
