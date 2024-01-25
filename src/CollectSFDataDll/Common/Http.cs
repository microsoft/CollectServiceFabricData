// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace CollectSFData.Common
{
    public class Http
    {
        private readonly HttpClient _httpClient;

        //private readonly CustomTaskManager _httpTasks = new CustomTaskManager();

        public HttpContentHeaders Headers { get; set; }

        public HttpMethod Method { get; set; } = HttpMethod.Get;

        public HttpResponseMessage Response { get; private set; }

        public JObject ResponseStreamJson { get; private set; }

        public string ResponseStreamString { get; private set; }

        public HttpStatusCode StatusCode { get; private set; }

        public bool Success { get; set; }

        private Http()
        {
#if NET462
            _httpClient = new HttpClient();
#else
            _httpClient = new HttpClient(new HttpClientHandler()
            {
                CheckCertificateRevocationList = true
            });
#endif
            _httpClient.Timeout = Timeout.InfiniteTimeSpan;
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        public static Http ClientFactory()
        {
            return new Http();
        }

        public bool CheckConnectivity(string uri, string authToken = null, Dictionary<string, string> headers = null)
        {
            Log.Info($"enter: {uri}", ConsoleColor.Magenta);
            bool result = false;
            if(headers == null)
            {
                headers = new Dictionary<string, string>();
            }
            if(!headers.ContainsKey("User-Agent"))
            {
                headers.Add("User-Agent", "CollectSFData");
            }

            if (SendRequest(uri: uri, authToken: authToken, httpMethod: HttpMethod.Head, headers: headers, displayError: false))
            {
                result = StatusCode == HttpStatusCode.OK;
            }

            Log.Info($"exit: {uri} result: {result}", ConsoleColor.Magenta);
            return result;
        }

        public bool SendRequest(
            string uri,
            string authToken = null,
            string jsonBody = null,
            HttpMethod httpMethod = null,
            Dictionary<string, string> headers = null,
            HttpStatusCode okStatus = HttpStatusCode.OK,
            bool expectJsonResult = true,
            bool displayError = true)
        {
            HttpContent httpContent = default(HttpContent);
            httpMethod = httpMethod ?? Method;
            HttpCompletionOption httpCompletionOption = HttpCompletionOption.ResponseContentRead;

            Log.Info($"enter:method: {httpMethod} uri: {uri}", ConsoleColor.Magenta, ConsoleColor.Black);

            try
            {
                if (!string.IsNullOrEmpty(jsonBody))
                {
                    byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonBody);
                    httpContent = new ByteArrayContent(jsonBytes);
                    httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    httpContent.Headers.ContentLength = jsonBytes.Length;

                    Log.Info($"json bytes:{jsonBytes.Length} uri:{uri}", ConsoleColor.Magenta, ConsoleColor.Black);
                }

                // head not working with httpclient use get with ResponseHeadersRead
                if(httpMethod == HttpMethod.Head)
                {
                    httpMethod = HttpMethod.Get;
                    _httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
                    httpCompletionOption = HttpCompletionOption.ResponseHeadersRead;
                }

                HttpRequestMessage request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(uri),
                    Method = httpMethod,
                    Content = httpContent,
                };

                if (authToken != null)
                {
                    request.Headers.Add("Authorization", $"Bearer {authToken}");
                }

                if (headers != null)
                {
                    foreach (KeyValuePair<string, string> header in headers)
                    {
                        request.Headers.Add(header.Key, header.Value);
                    }
                }

                //Response = _httpTasks.TaskFunction((httpResponse) => _httpClient.SendAsync(request).Result).Result as HttpResponseMessage;
                Response = _httpClient.SendAsync(request, httpCompletionOption).Result;

                Log.Info($"response status: {Response.StatusCode}", ConsoleColor.DarkMagenta, ConsoleColor.Black);
                StatusCode = Response.StatusCode;

                if (Response.Content.Headers.ContentLength > 0)
                {
                    ResponseStreamString = Response.Content.ReadAsStringAsync().Result;

                    if (expectJsonResult && !string.IsNullOrEmpty(ResponseStreamString))
                    {
                        ResponseStreamJson = JObject.Parse(ResponseStreamString);
                        Log.Debug($"WebResponse stream: bytes: {Response.Content.Headers.ContentLength}\r\n{ResponseStreamJson}");
                    }
                }
                else
                {
                    ResponseStreamJson = new JObject();
                    ResponseStreamString = string.Empty;
                    Log.Info("no responseStream");
                }

                Log.Debug("response headers:", Response.Headers);

                if (Response.IsSuccessStatusCode || Response.StatusCode == okStatus)
                {
                    return Success = true;
                }

                if (displayError)
                {
                    Log.Error("unsuccessful response:", Response);
                }

                return Success = false;
            }
            catch (WebException we)
            {
                StatusCode = ((HttpWebResponse)we.Response).StatusCode;
                Log.Error($"webexception: {we.Status}: {(int)StatusCode}", we.Response);
                return Success = false;
            }
            catch (Exception e)
            {
                if (displayError)
                {
                    Log.Exception($"post exception:{e}");
                }

                return Success = false;
            }
        }
    }
}