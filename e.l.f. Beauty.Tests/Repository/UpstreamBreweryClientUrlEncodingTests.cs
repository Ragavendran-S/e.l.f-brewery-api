using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using e.l.f._Beauty.Models;
using e.l.f._Beauty.Repository;
using Xunit;

namespace e.l.f._Beauty.Tests.Repository
{
    public class UpstreamBreweryClientUrlEncodingTests
    {
        private class RecordingHandler : HttpMessageHandler
        {
            public HttpRequestMessage? LastRequest { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequest = request;
                var resp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", Encoding.UTF8, "application/json")
                };
                return Task.FromResult(resp);
            }
        }

        [Fact]
        public async Task GetBreweryByNameAsync_EncodesNameInQuery()
        {
            var handler = new RecordingHandler();
            var client = new HttpClient(handler);
            var upstream = new UpstreamBreweryClient(client);

            var name = "Acme & Sons/Co";
            await upstream.GetBreweryByNameAsync(name);

            Assert.NotNull(handler.LastRequest);
            var uri = handler.LastRequest!.RequestUri!;
            // Expect by_name query parameter to be present and round-trip to the original value
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
            Assert.True(query.ContainsKey("by_name"));
            Assert.Equal(name, query["by_name"].ToString());
        }

        [Fact]
        public async Task SearchBreweriesAsync_EncodesQueryInAutocomplete()
        {
            var handler = new RecordingHandler();
            var client = new HttpClient(handler);
            var upstream = new UpstreamBreweryClient(client);

            var q = "La & Co?";
            await upstream.SearchBreweriesAsync(q);

            Assert.NotNull(handler.LastRequest);
            var uri = handler.LastRequest!.RequestUri!;
            Assert.Contains("/autocomplete", uri.AbsolutePath);
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
            Assert.True(query.ContainsKey("query"));
            Assert.Equal(q, query["query"].ToString());
        }

        [Fact]
        public async Task GetBreweriesAsync_WithSearch_UsesAutocompleteEndpoint()
        {
            var handler = new RecordingHandler();
            var client = new HttpClient(handler);
            var upstream = new UpstreamBreweryClient(client);

            var options = new BreweryQueryOptions { Search = "Test & Co" };
            await upstream.GetBreweriesAsync(options);

            Assert.NotNull(handler.LastRequest);
            var uri = handler.LastRequest!.RequestUri!;
            Assert.Contains("/autocomplete", uri.AbsolutePath);
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
            Assert.True(query.ContainsKey("query"));
            Assert.Equal(options.Search, query["query"].ToString());
        }
    }
}
