using System.Net;
using RaceDay.Web.Services;

namespace RaceDay.Web.Tests;

public sealed class RaceDayApiClientTests
{
    [Fact]
    public async Task SendAsync_CombinesBaseAddressWithRelativeApiPath()
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost:7107/")
        };
        var api = new RaceDayApiClient(httpClient);

        using var response = await api.SendAsync(HttpMethod.Get, "api/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new Uri("https://localhost:7107/api/events"), handler.RequestUri);
    }

    [Theory]
    [InlineData("https://example.com/api/events")]
    [InlineData("//example.com/api/events")]
    [InlineData("/api/events")]
    [InlineData("../other-api")]
    [InlineData("api\\events")]
    public async Task SendAsync_RejectsPathsThatCouldEscapeTheConfiguredApi(string path)
    {
        using var httpClient = new HttpClient(new RecordingHandler())
        {
            BaseAddress = new Uri("https://localhost:7107/")
        };
        var api = new RaceDayApiClient(httpClient);

        await Assert.ThrowsAsync<ArgumentException>(() => api.SendAsync(HttpMethod.Get, path));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
