﻿using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using netmockery;
using netmockery.Controllers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace UnitTests
{
    public class TestWebUI
    {
        private ITestOutputHelper output;
        public TestServer server;
        public HttpClient client;

        public TestWebUI(ITestOutputHelper output)
        {
            this.output = output;
            Program.UnitTest_SetConfigDirectory("examples\\example1");
            server = new TestServer(new WebHostBuilder().UseContentRoot("..\\..\\..\\..\\..\\netmockery").UseStartup<Startup>());
            client = server.CreateClient();
        }
        [Fact]
        public async Task RootRedirectsToHome()
        {
            var response = await client.GetAsync("/");
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/__netmockery/Home", response.Headers.Location.ToString());
        }

        [Fact]
        public async Task HomeRedirectsToEndpoints()
        {
            var response = await client.GetAsync("/__netmockery/Home");
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/__netmockery", response.Headers.Location.ToString());
        }

        const int EXPECTED_NUMBER_OF_URLS = 16;
        const int EXPECTED_EXTRA_URLS_PER_REQUEST = 2;

        [Fact]
        public async Task CheckAllUrlsNoRequestsMade()
        {
            var urls = await visitAllUrls("/__netmockery");
            Assert.Equal(EXPECTED_NUMBER_OF_URLS, urls.Count);
        }

        [Fact]
        public async Task CheckAllUrlsAfterTestRequestsMade()
        {
            Assert.Equal(0, Startup.ResponseRegistry.Responses.Count());
            var testRunner = new WebTestRunner(Program.EndpointCollection);
            Assert.True(testRunner.Tests.Count() > 0);
            foreach (var test in testRunner.Tests)
            {
                var result = await test.ExecuteAgainstHttpClientAsync(client, test.RequestPath);
                Assert.True(result.OK);
            }

            var urls = await visitAllUrls("/__netmockery", includeReloadConfig: false);
            Assert.Equal(EXPECTED_NUMBER_OF_URLS + testRunner.Tests.Count() * EXPECTED_EXTRA_URLS_PER_REQUEST, urls.Count);
            Assert.Equal(testRunner.Tests.Count(), Startup.ResponseRegistry.Responses.Count());
        }

        [Fact]
        public async Task CheckAllUrlsAfterFailingRequests()
        {
            Assert.Equal(0, Startup.ResponseRegistry.Responses.Count());

            var response = await client.GetAsync("/lkj/");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            Assert.Equal("", content);
            Assert.Equal(1, Startup.ResponseRegistry.Responses.Count());
            var registryItem = Startup.ResponseRegistry.Responses.Single();
            Assert.Equal("No endpoint matches request path", registryItem.Error);

            var urls = await visitAllUrls("/__netmockery", includeReloadConfig: false);
            Assert.Equal(EXPECTED_NUMBER_OF_URLS + Startup.ResponseRegistry.Responses.Count() * EXPECTED_EXTRA_URLS_PER_REQUEST, urls.Count);
        }

        [Fact]
        public async Task CheckAllUrlsAfterNoMatchInEndpoint()
        {
            Assert.Equal(0, Startup.ResponseRegistry.Responses.Count());

            var response = await client.GetAsync("/endpoint2");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            Assert.Equal("", content);
            Assert.Equal(1, Startup.ResponseRegistry.Responses.Count());

            var registryItem = Startup.ResponseRegistry.Responses.Single();
            Assert.Equal("Endpoint has no match for request", registryItem.Error);

            var urls = await visitAllUrls("/__netmockery", includeReloadConfig: false);
            Assert.Equal(EXPECTED_NUMBER_OF_URLS + Startup.ResponseRegistry.Responses.Count() * EXPECTED_EXTRA_URLS_PER_REQUEST, urls.Count);

        }

        [Fact]
        public async Task EndpointsListed()
        {
            var response = await client.GetAsync("/__netmockery");
            response.EnsureSuccessStatusCode();
        }
        private async Task<List<string>> visitAllUrls(string start, bool includeReloadConfig = true)
        {
            List<string> visited = new List<string>();

            if (! includeReloadConfig)
            {
                visited.Add("/__netmockery/Endpoints/ReloadConfig");
            }

            await visitUrls(visited, start);

            return visited;
        }

        private async Task visitUrls(List<string> visited, string url)
        {
            if (visited.Contains(url))
            {
                return;
            }

            visited.Add(url);
            output.WriteLine(url);
            var response = await client.GetAsync(url);

            if (response.StatusCode == HttpStatusCode.Redirect)
            {
                await visitUrls(visited, response.Headers.Location.ToString());
            }
            else
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();

                var hrefs = Regex.Matches(content, "href=\"(.*?)\"");
                foreach (var hrefMatch in hrefs.Cast<Match>())
                {
                    var href = hrefMatch.Groups[1].Value;
                    if (!href.StartsWith("/"))
                    {
                        continue;
                    }
                    await visitUrls(visited, href);
                }
            }
        }

    }
}
