namespace Ocelot.AcceptanceTests
{
    using Configuration;
    using Microsoft.AspNetCore.Http;
    using Ocelot.Configuration.File;
    using Requester;
    using Shouldly;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Net;
    using TestStack.BDDfy;
    using Xunit;

    public class HttpClientCachingTests : IDisposable
    {
        private readonly Steps _steps;
        private string _downstreamPath;
        private readonly ServiceHandler _serviceHandler;

        public HttpClientCachingTests()
        {
            _serviceHandler = new ServiceHandler();
            _steps = new Steps();
        }

        [Fact]
        public void should_cache_one_http_client_same_re_route()
        {
            var configuration = new FileConfiguration
            {
                ReRoutes = new List<FileReRoute>
                    {
                        new FileReRoute
                        {
                            DownstreamPathTemplate = "/",
                            DownstreamScheme = "http",
                            DownstreamHostAndPorts = new List<FileHostAndPort>
                            {
                                new FileHostAndPort
                                {
                                    Host = "localhost",
                                    Port = 58814,
                                }
                            },
                            UpstreamPathTemplate = "/",
                            UpstreamHttpMethod = new List<string> { "Get" },
                        }
                    }
            };

            var cache = new FakeHttpClientCache();

            this.Given(x => x.GivenThereIsAServiceRunningOn("http://localhost:58814", 200, "Hello from Laura"))
                .And(x => _steps.GivenThereIsAConfiguration(configuration))
                .And(x => _steps.GivenOcelotIsRunningWithFakeHttpClientCache(cache))
                .When(x => _steps.WhenIGetUrlOnTheApiGateway("/"))
                .When(x => _steps.WhenIGetUrlOnTheApiGateway("/"))
                .When(x => _steps.WhenIGetUrlOnTheApiGateway("/"))
                .When(x => _steps.WhenIGetUrlOnTheApiGateway("/"))
                .Then(x => _steps.ThenTheStatusCodeShouldBe(HttpStatusCode.OK))
                .And(x => _steps.ThenTheResponseBodyShouldBe("Hello from Laura"))
                .And(x => cache.Count.ShouldBe(1))
                .BDDfy();
        }

        [Fact]
        public void should_cache_two_http_client_different_re_route()
        {
            var configuration = new FileConfiguration
            {
                ReRoutes = new List<FileReRoute>
                {
                    new FileReRoute
                    {
                        DownstreamPathTemplate = "/",
                        DownstreamScheme = "http",
                        DownstreamHostAndPorts = new List<FileHostAndPort>
                        {
                            new FileHostAndPort
                            {
                                Host = "localhost",
                                Port = 58817,
                            }
                        },
                        UpstreamPathTemplate = "/",
                        UpstreamHttpMethod = new List<string> { "Get" },
                    },
                    new FileReRoute
                    {
                        DownstreamPathTemplate = "/two",
                        DownstreamScheme = "http",
                        DownstreamHostAndPorts = new List<FileHostAndPort>
                        {
                            new FileHostAndPort
                            {
                                Host = "localhost",
                                Port = 58817,
                            }
                        },
                        UpstreamPathTemplate = "/two",
                        UpstreamHttpMethod = new List<string> { "Get" },
                    }
                }
            };

            var cache = new FakeHttpClientCache();

            this.Given(x => x.GivenThereIsAServiceRunningOn("http://localhost:58817", 200, "Hello from Laura"))
                .And(x => _steps.GivenThereIsAConfiguration(configuration))
                .And(x => _steps.GivenOcelotIsRunningWithFakeHttpClientCache(cache))
                .When(x => _steps.WhenIGetUrlOnTheApiGateway("/"))
                .When(x => _steps.WhenIGetUrlOnTheApiGateway("/two"))
                .When(x => _steps.WhenIGetUrlOnTheApiGateway("/two"))
                .When(x => _steps.WhenIGetUrlOnTheApiGateway("/"))
                .When(x => _steps.WhenIGetUrlOnTheApiGateway("/"))
                .When(x => _steps.WhenIGetUrlOnTheApiGateway("/two"))
                .When(x => _steps.WhenIGetUrlOnTheApiGateway("/"))
                .Then(x => _steps.ThenTheStatusCodeShouldBe(HttpStatusCode.OK))
                .And(x => _steps.ThenTheResponseBodyShouldBe("Hello from Laura"))
                .And(x => cache.Count.ShouldBe(2))
                .BDDfy();
        }

        private void GivenThereIsAServiceRunningOn(string baseUrl, int statusCode, string responseBody)
        {
            _serviceHandler.GivenThereIsAServiceRunningOn(baseUrl, async context =>
            {
                context.Response.StatusCode = statusCode;
                await context.Response.WriteAsync(responseBody);
            });
        }

        public void Dispose()
        {
            _serviceHandler.Dispose();
            _steps.Dispose();
        }

        public class FakeHttpClientCache : IHttpClientCache
        {
            private readonly ConcurrentDictionary<DownstreamReRoute, IHttpClient> _httpClientsCache;

            public FakeHttpClientCache()
            {
                _httpClientsCache = new ConcurrentDictionary<DownstreamReRoute, IHttpClient>();
            }

            public void Set(DownstreamReRoute key, IHttpClient client, TimeSpan expirationTime)
            {
                _httpClientsCache.AddOrUpdate(key, client, (k, oldValue) => client);
            }

            public IHttpClient Get(DownstreamReRoute key)
            {
                //todo handle error?
                return _httpClientsCache.TryGetValue(key, out var client) ? client : null;
            }

            public int Count => _httpClientsCache.Count;
        }
    }
}
