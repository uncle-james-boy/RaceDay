using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Tests;

public sealed class RaceDayApiClientTests
{
    [Fact]
    public async Task SendAsync_CombinesBaseAddressWithRelativeApiPath()
    {
        var handler = new RecordingHandler();
        var (session, _) = TestSessions.Create();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost:7107/")
        };
        var api = new RaceDayApiClient(httpClient, session);

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
        var (session, _) = TestSessions.Create();
        var api = new RaceDayApiClient(httpClient, session);

        await Assert.ThrowsAsync<ArgumentException>(() => api.SendAsync(HttpMethod.Get, path));
    }

    [Fact]
    public async Task PostFileAsyncForwardsMultipartAndOnlyTheCurrentUsersApiSession()
    {
        var handler = new MultipartHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:7107/") };
        var (session, _) = TestSessions.Create();
        session.SignIn(new ApiUserResponse(7, "Demo Organiser", "organiser@example.test", "Organiser", false), "organiser-session");
        var api = new RaceDayApiClient(httpClient, session);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("banner bytes"));
        var file = new FormFile(stream, 0, stream.Length, "Event.BannerFile", "banner.png")
        {
            Headers = new HeaderDictionary { ["Content-Type"] = "image/png" }
        };

        var result = await api.PostFileAsync<EventBannerUploadResponse>("api/events/12/banner", file);

        Assert.True(result.IsSuccess);
        Assert.Equal(12, result.Value!.EventID);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("/api/events/12/banner", handler.Path);
        Assert.Equal("organiser-session", handler.Cookie);
        Assert.Equal("multipart/form-data", handler.MediaType);
        Assert.True(handler.Body.Contains("name=\"file\"", StringComparison.Ordinal)
            || handler.Body.Contains("name=file", StringComparison.Ordinal));
        Assert.True(handler.Body.Contains("filename=\"banner.png\"", StringComparison.Ordinal)
            || handler.Body.Contains("filename=banner.png", StringComparison.Ordinal));
        Assert.Contains("banner bytes", handler.Body);
    }

    [Fact]
    public async Task GetFileAsyncForwardsTheCurrentSessionCookieAndReturnsSupportedImageBytes()
    {
        var handler = new FileHandler(HttpStatusCode.OK, "image/webp", [1, 2, 3]);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:7107/") };
        var (session, _) = TestSessions.Create();
        session.SignIn(new ApiUserResponse(44, "Demo Runner", "runner@example.test", "Participant", true), "participant-session");
        var api = new RaceDayApiClient(httpClient, session);

        var result = await api.GetFileAsync("api/users/profile/picture");

        Assert.True(result.IsSuccess);
        Assert.Equal("image/webp", result.ContentType);
        Assert.Equal(new byte[] { 1, 2, 3 }, result.Content);
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal("/api/users/profile/picture", handler.Path);
        Assert.Equal("participant-session", handler.Cookie);
    }

    [Fact]
    public async Task GetFileAsyncRejectsUnsupportedContentTypes()
    {
        var handler = new FileHandler(HttpStatusCode.OK, "text/html", [1, 2, 3]);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:7107/") };
        var (session, _) = TestSessions.Create();
        session.SignIn(new ApiUserResponse(44, "Demo Runner", "runner@example.test", "Participant", true), "participant-session");
        var api = new RaceDayApiClient(httpClient, session);

        var result = await api.GetFileAsync("api/users/profile/picture");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Content);
        Assert.Equal("The API returned an unsupported image format.", result.ErrorMessage);
    }

    [Fact]
    public async Task LoginAsync_AcceptsPercentAndPreservesCookieValue()
    {
        const string cookie = "session%2Fvalue";
        using var httpClient = CreateLoginClient(cookie);
        var (session, _) = TestSessions.Create();
        var api = new RaceDayApiClient(httpClient, session);

        var result = await api.LoginAsync(new { Email = "runner@example.test", Password = "not-logged" });

        Assert.True(result.IsSuccess);
        Assert.Equal(cookie, session.ApiSessionCookie);
    }

    [Fact]
    public void ApiSessionContext_AcceptsAllRfc6265CookieOctetRanges()
    {
        var cookie = string.Concat(new (int Start, int End)[]
            {
                (0x21, 0x21), (0x23, 0x2B), (0x2D, 0x3A), (0x3C, 0x5B), (0x5D, 0x7E)
            }
            .SelectMany(range => Enumerable.Range(range.Start, range.End - range.Start + 1))
            .Select(codePoint => (char)codePoint));
        var (session, _) = TestSessions.Create();

        session.SignIn(TestUser(), cookie);

        Assert.Equal(cookie, session.ApiSessionCookie);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\r")]
    [InlineData("\n")]
    [InlineData("\u0000")]
    [InlineData("\u001F")]
    [InlineData("\u007F")]
    [InlineData("\"")]
    [InlineData(",")]
    [InlineData(";")]
    [InlineData("\\")]
    [InlineData("é")]
    [InlineData("\u0080")]
    [InlineData("😀")]
    public void ApiSessionContext_RejectsForbiddenCookieCharacters(string cookie)
    {
        var (session, _) = TestSessions.Create();

        Assert.Throws<ArgumentException>(() => session.SignIn(TestUser(), $"a{cookie}b"));
    }

    [Fact]
    public void ApiSessionContext_RejectsEmptyCookie()
    {
        var (session, _) = TestSessions.Create();

        Assert.Throws<ArgumentException>(() => session.SignIn(TestUser(), string.Empty));
    }

    [Fact]
    public void ApiSessionContext_Accepts512CharacterCookie()
    {
        const int cookieLength = 512;
        var cookie = new string('a', cookieLength);
        var (session, _) = TestSessions.Create();

        session.SignIn(TestUser(), cookie);

        Assert.Equal(cookie, session.ApiSessionCookie);
    }

    [Fact]
    public void ApiSessionContext_Rejects513CharacterCookie()
    {
        var (session, _) = TestSessions.Create();

        Assert.Throws<ArgumentException>(() => session.SignIn(TestUser(), new string('a', 513)));
    }

    [Fact]
    public async Task LoginAsync_LogsDistinctRejectedCodePointsWithoutCookieValue()
    {
        const string cookie = "unsafe%cookie%é";
        var logger = new CapturingLogger();
        using var httpClient = CreateLoginClient(cookie);
        var (session, _) = TestSessions.Create();
        var api = new RaceDayApiClient(httpClient, session, logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            api.LoginAsync(new { Email = "runner@example.test", Password = "not-logged" }));

        var diagnostic = Assert.Single(logger.Messages);
        Assert.Contains("ValueLength=15", diagnostic);
        Assert.Contains("ContainsDisallowedCharacters=True", diagnostic);
        Assert.Contains("Exceeds512CharacterLimit=False", diagnostic);
        Assert.Contains("DisallowedUnicodeCodePoints=[233]", diagnostic);
        Assert.DoesNotContain(cookie, diagnostic);
        Assert.DoesNotContain("%", diagnostic);
        Assert.DoesNotContain("é", diagnostic);
    }

    [Fact]
    public async Task LoginAsync_LogsSafeDiagnosticsWhenCookieExceedsLimit()
    {
        var cookie = new string('a', 513);
        var logger = new CapturingLogger();
        using var httpClient = CreateLoginClient(cookie);
        var (session, _) = TestSessions.Create();
        var api = new RaceDayApiClient(httpClient, session, logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            api.LoginAsync(new { Email = "runner@example.test", Password = "not-logged" }));

        var diagnostic = Assert.Single(logger.Messages);
        Assert.Contains("ValueLength=513", diagnostic);
        Assert.Contains("ContainsDisallowedCharacters=False", diagnostic);
        Assert.Contains("Exceeds512CharacterLimit=True", diagnostic);
        Assert.DoesNotContain(cookie, diagnostic);
    }

    private static HttpClient CreateLoginClient(string cookie) => new(new LoginHandler(cookie))
    {
        BaseAddress = new Uri("https://localhost:7107/")
    };

    private static ApiUserResponse TestUser() =>
        new(1, "Demo Runner", "runner@example.test", "Participant", false);

    private sealed class LoginHandler(string cookie) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new ApiUserResponse(
                    1, "Demo Runner", "runner@example.test", "Participant", false))
            };
            response.Headers.TryAddWithoutValidation(
                "Set-Cookie",
                $"{ApiSessionContext.ApiSessionCookieName}={cookie}; path=/; httponly; samesite=lax");
            return Task.FromResult(response);
        }
    }

    private sealed class CapturingLogger : ILogger<RaceDayApiClient>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
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

    private sealed class MultipartHandler : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public string? Path { get; private set; }
        public string? Cookie { get; private set; }
        public string? MediaType { get; private set; }
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            Path = request.RequestUri!.AbsolutePath;
            Cookie = request.Headers.TryGetValues("Cookie", out var values)
                ? values.Single().Split('=', 2)[1]
                : null;
            MediaType = request.Content?.Headers.ContentType?.MediaType;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new EventBannerUploadResponse(12,
                    "https://raceday.blob.core.windows.net/event-banners/banner.png"))
            };
        }
    }

    private sealed class FileHandler(HttpStatusCode statusCode, string contentType, byte[] bytes) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public string? Path { get; private set; }
        public string? Cookie { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            Path = request.RequestUri!.AbsolutePath;
            Cookie = request.Headers.TryGetValues("Cookie", out var values)
                ? values.Single().Split('=', 2)[1]
                : null;
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            return Task.FromResult(new HttpResponseMessage(statusCode) { Content = content });
        }
    }
}
