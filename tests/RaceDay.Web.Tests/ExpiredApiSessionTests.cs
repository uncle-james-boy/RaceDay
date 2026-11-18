using System.Net;
using Microsoft.AspNetCore.Http;
using RaceDay.Web.Security;
using RaceDay.Web.Services;

namespace RaceDay.Web.Tests;

public sealed class ExpiredApiSessionTests
{
    [Fact]
    public async Task ApiUnauthorizedClearsMvcSessionAndRedirectsToLoginWithNotice()
    {
        var (session, _) = TestSessions.Create();
        session.SignIn(new ApiUserResponse(9, "Demo", "demo@example.test", "Participant", false), "expired-cookie");
        using var client = new HttpClient(new UnauthorizedHandler())
        {
            BaseAddress = new Uri("https://localhost:7107/")
        };
        var api = new RaceDayApiClient(client, session);
        var context = new DefaultHttpContext();
        var middleware = new ApiSessionExpiredMiddleware(async _ =>
        {
            using var response = await api.SendAsync(HttpMethod.Get, "api/users/profile");
        });

        await middleware.InvokeAsync(context);

        Assert.False(session.IsAuthenticated);
        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/Auth/Login?sessionExpired=true", context.Response.Headers.Location);
    }

    private sealed class UnauthorizedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
    }
}
