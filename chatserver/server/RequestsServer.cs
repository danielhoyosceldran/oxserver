using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace chatserver.server
{
    internal class RequestsServer
    {
        public int Port = 8081;

        private HttpListener _listener;

        public void Start()
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

        private void Receive()
        {
            _listener.BeginGetContext(new AsyncCallback(ListenerCallback_BasicAuth), _listener);
        }

        private bool Authenticate(HttpListenerRequest request)
        {
            if (request.Headers["Authorization"] != null)
            {
                string authHeader = request.Headers["Authorization"];
                if (authHeader.StartsWith("Basic"))
                {
                    string encodedCredentials = authHeader.Substring("Basic ".Length).Trim();
                    string credentials = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCredentials));
                    string[] parts = credentials.Split(':');
                    string username = parts[0];
                    string password = parts[1];

                    if (username == "user" && password == "pass")
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void ListenerCallback_BasicAuth(IAsyncResult result)
        {
            if (_listener.IsListening)
            {
                var context = _listener.EndGetContext(result);
                var request = context.Request;

                var response = context.Response;
                if (Authenticate(request))
                {
                    response.StatusCode = (int)HttpStatusCode.OK;
                    response.ContentType = "text/plain";
                    string responseBody = "Autenticat!";
                    byte[] buffer = Encoding.UTF8.GetBytes(responseBody);
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                    response.OutputStream.Close();
                    Console.WriteLine($"{request.HttpMethod} {request.Url} - OK");
                    if (request.HasEntityBody)
                    {
                        var body = request.InputStream;
                        var encoding = request.ContentEncoding;
                        var reader = new StreamReader(body, encoding);
                        if (request.ContentType != null)
                        {
                            Console.WriteLine("Client data content type {0}", request.ContentType);
                        }
                        Console.WriteLine("Client data content length {0}", request.ContentLength64);

                        Console.WriteLine("Start of data:");
                        string s = reader.ReadToEnd();
                        Console.WriteLine(s);
                        Console.WriteLine("End of data:");
                        reader.Close();
                        body.Close();
                    }
                }
                else
                {
                    Console.WriteLine($"error");
                    response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    response.AddHeader("WWW-Authenticate", "Basic realm=\"MyServer\"");
                    string responseString = "<html><body>401 Unauthorized</body></html>";
                    byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                }

                Receive();
            }
        }

        private void ListenerCallback_NoAuth(IAsyncResult result)
        {
            if (_listener.IsListening)
            {
                var context = _listener.EndGetContext(result);
                var request = context.Request;

                var response = context.Response;
                response.StatusCode = (int)HttpStatusCode.OK;
                response.ContentType = "text/plain";
                string responseBody = "Autenticat!";
                byte[] buffer = Encoding.UTF8.GetBytes(responseBody);
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();
                Console.WriteLine($"{request.HttpMethod} {request.Url} - OK");
                if (request.HasEntityBody)
                {
                    var body = request.InputStream;
                    var encoding = request.ContentEncoding;
                    var reader = new StreamReader(body, encoding);
                    if (request.ContentType != null)
                    {
                        Console.WriteLine("Client data content type {0}", request.ContentType);
                    }
                    Console.WriteLine("Client data content length {0}", request.ContentLength64);

                    Console.WriteLine("Start of data:");
                    string s = reader.ReadToEnd();
                    Console.WriteLine(s);
                    Console.WriteLine("End of data:");
                    reader.Close();
                    body.Close();
                }

                Receive();
            }
        }
    }
}
