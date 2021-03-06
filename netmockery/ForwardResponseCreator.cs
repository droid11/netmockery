﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Diagnostics;
//using Microsoft.Net.Http.Headers;

namespace netmockery
{
    /* netcoreapp1.1 for some reason does not implement WebProxy */
    public class NetApp11WebProxy : IWebProxy
    {
        private Uri _uri;
        public NetApp11WebProxy(Uri uri)
        {
            Debug.Assert(uri != null);
            _uri = uri;
        }

        public ICredentials Credentials { get; set; }

        public Uri GetProxy(Uri destination)
        {
            return _uri;
        }

        public bool IsBypassed(Uri host) => false;
    }

    public class ForwardResponseCreator : ResponseCreator
    {
        public string Url { get; set; }
        public string ProxyUrl { get; set; }
        public string StripPath { get; set; }

        public ForwardResponseCreator(string url, Endpoint endpoint) : base(endpoint)
        {
            Url = url;
        }

        public override string ToString()
        {
            var retval = $"Forward to {Url}";
            if (ProxyUrl != null)
            {
                retval += $" (over {ProxyUrl})";
            }
            return retval;
        }

        string[] HEADERS_TO_SKIP = new[] { "connection", "content-length", "content-type", "accept-encoding", "expect", "host" };

        public override async Task<byte[]> CreateResponseAsync(IHttpRequestWrapper request, byte[] body, IHttpResponseWrapper response, Endpoint endpoint)
        {
            var requestPath = request.Path.ToString();
            if (StripPath != null)
            {
                requestPath = Regex.Replace(requestPath, StripPath, "");
            }
            var httpMsg = new HttpRequestMessage(HttpMethod.Post, Url + requestPath);

            Debug.Assert(request.Headers != null);
            foreach (var header in request.Headers)
            {
                if (! HEADERS_TO_SKIP.Contains(header.Key.ToLower()))
                {
                    httpMsg.Headers.Add(header.Key, header.Value.ToArray());
                }
            }
            httpMsg.Content = new ByteArrayContent(body);
            if ((string) request.Headers["Content-Type"] != null)
            {
                httpMsg.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(request.Headers["Content-Type"]);
            }
            
            var httpClient = 
                ProxyUrl == null ? 
                new HttpClient() : 
                new HttpClient(new HttpClientHandler { UseProxy = true, Proxy = new NetApp11WebProxy(new Uri(ProxyUrl)) });

            var responseMessage = await httpClient.SendAsync(httpMsg);

            response.ContentType = responseMessage.Content.Headers.ContentType.ToString();
            response.HttpStatusCode = responseMessage.StatusCode;
            var responseBodyStream = await responseMessage.Content.ReadAsStreamAsync();
            var memoryStream = new MemoryStream();
            responseBodyStream.CopyTo(memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin);
            memoryStream.CopyTo(response.Body);
            return memoryStream.ToArray();
        }
    }
}
