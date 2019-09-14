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
using System.Text;
using System.Threading;

namespace CollectSFData
{
    public class Http : Constants
    {
        private static readonly HttpClient _httpClient;

        private static readonly CustomTaskManager _httpTasks = new CustomTaskManager(true);

        static Http()
        {
            _httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        public HttpContentHeaders Headers { get; set; }

        public HttpMethod Method { get; set; } = HttpMethod.Get;

        public HttpResponseMessage Response { get; private set; }

        public JObject ResponseStreamJson { get; private set; }

        public string ResponseStreamString { get; private set; }

        public HttpStatusCode StatusCode { get; private set; }

        public bool Success { get; set; }

        public static Http ClientFactory()
        {
            return new Http();
        }

        public bool SendRequest(
            string uri,
            string authToken = null,
            string jsonBody = null,
            HttpMethod httpMethod = null,
            Dictionary<string, string> headers = null,
            HttpStatusCode okStatus = HttpStatusCode.OK)
        {
            HttpContent httpContent = default(HttpContent);
            httpMethod = httpMethod ?? Method;
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

                Response = _httpTasks.TaskFunction((httpresponse) => _httpClient.SendAsync(request).Result).Result as HttpResponseMessage;
                Log.Info($"response status: {Response.StatusCode}", ConsoleColor.DarkMagenta, ConsoleColor.Black);
                StatusCode = Response.StatusCode;

                if (Response.Content.Headers.ContentLength > 0)
                {
                    ResponseStreamString = Response.Content.ReadAsStringAsync().Result;

                    if (!string.IsNullOrEmpty(ResponseStreamString))
                    {
                        ResponseStreamJson = JObject.Parse(ResponseStreamString);
                        Log.Info($"WebResponse stream: bytes: {Response.Content.Headers.ContentLength}\r\n{ResponseStreamJson}", ConsoleColor.DarkMagenta, ConsoleColor.Black);
                    }
                }
                else
                {
                    Log.Info("no responseStream");
                }

                Log.Debug("response headers:", Response.Headers);

                if (Response.IsSuccessStatusCode || Response.StatusCode == okStatus)
                {
                    return Success = true;
                }

                Log.Error("unsuccessful response:", Response);
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
                Log.Exception($"post exception:{e}");
                return Success = false;
            }
        }
    }
}