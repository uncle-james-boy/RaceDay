using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RaceDay.Web.Services;

namespace RaceDay.Web.Tests;

public sealed class ApiSessionIsolationTests
{
    [Fact]
    public async Task LoginStoresServerSideApiCookieAndUsesItOnFollowingRequest()
    {
        var handler = new UserSessionHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:7107/") };
        var (session, backingSession) = TestSessions.Create();
        var api = new RaceDayApiClient(httpClient, session);

        var login = await api.LoginAsync(new { Email = "runner@example.test", Password = "demo" });
        using var response = await api.SendAsync(HttpMethod.Get, "api/users/profile");

        Assert.True(login.IsSuccess);
        Assert.Equal(41, session.UserId);
        Assert.Equal("Participant", session.Role);
        Assert.Equal("session-runner", session.ApiSessionCookie);
        Assert.Equal("session-runner", handler.LastApiCookie);
        Assert.Contains("RaceDay.ApiCookie", backingSession.Keys);
        Assert.DoesNotContain(ApiSessionContext.ApiSessionCookieName, backingSession.Keys);
    }

    [Fact]
    public async Task TwoMvcSessionsForwardOnlyTheirOwnApiCookie()
    {
        var handler = new UserSessionHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:7107/") };
        var (organiserSession, _) = TestSessions.Create();
        var (participantSession, _) = TestSessions.Create();
        var organiserApi = new RaceDayApiClient(httpClient, organiserSession);
        var participantApi = new RaceDayApiClient(httpClient, participantSession);

        await organiserApi.LoginAsync(new { Email = "organiser@example.test", Password = "demo" });
        await participantApi.LoginAsync(new { Email = "participant@example.test", Password = "demo" });
        using var organiserResponse = await organiserApi.SendAsync(HttpMethod.Get, "api/events");
        var organiserCookie = handler.LastApiCookie;
        using var participantResponse = await participantApi.SendAsync(HttpMethod.Get, "api/enrolments");
        var participantCookie = handler.LastApiCookie;

        Assert.Equal("session-organiser", organiserCookie);
        Assert.Equal("session-participant", participantCookie);
        Assert.NotEqual(organiserCookie, participantCookie);
    }

    [Fact]
    public async Task LoginDoesNotAcceptSuccessfulResponseWithoutApiSessionCookie()
    {
        using var httpClient = new HttpClient(new MissingCookieHandler())
        {
            BaseAddress = new Uri("https://localhost:7107/")
        };
        var (session, _) = TestSessions.Create();
        var api = new RaceDayApiClient(httpClient, session);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            api.LoginAsync(new { Email = "runner@example.test", Password = "demo" }));

        Assert.False(session.IsAuthenticated);
    }

    [Fact]
    public async Task UnauthenticatedRequestDoesNotForwardAnExistingSessionCookie()
    {
        var handler = new UserSessionHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:7107/") };
        var (session, _) = TestSessions.Create();
        session.SignIn(new ApiUserResponse(8, "Participant", "p@example.test", "Participant", null), "private-session");
        var api = new RaceDayApiClient(httpClient, session);

        using var response = await api.SendUnauthenticatedAsync(HttpMethod.Post, "api/auth/register", JsonContent.Create(new { }));

        Assert.Null(handler.LastApiCookie);
    }

    private sealed class UserSessionHandler : HttpMessageHandler
    {
        public string? LastApiCookie { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastApiCookie = request.Headers.TryGetValues("Cookie", out var cookies)
                ? cookies.Single().Split('=', 2)[1]
                : null;

            if (request.RequestUri?.AbsolutePath == "/api/auth/login")
            {
                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                var email = JsonDocument.Parse(body).RootElement.GetProperty("email").GetString()!;
                var isOrganiser = email.StartsWith("organiser", StringComparison.Ordinal);
                var cookie = isOrganiser ? "session-organiser" : email.StartsWith("participant", StringComparison.Ordinal)
                    ? "session-participant"
                    : "session-runner";
                var user = new ApiUserResponse(
                    isOrganiser ? 7 : 41,
                    isOrganiser ? "Organiser" : "Participant",
                    email,
                    isOrganiser ? "Organiser" : "Participant",
                    null);
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(user)
                };
                response.Headers.TryAddWithoutValidation(
                    "Set-Cookie",
                    $"{ApiSessionContext.ApiSessionCookieName}={cookie}; path=/; httponly; samesite=lax");
                return response;
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class MissingCookieHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new ApiUserResponse(41, "Participant", "p@example.test", "Participant", null))
            });
    }
}
