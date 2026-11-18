using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using RaceDay.Web.Security;

namespace RaceDay.Web.Tests;

public sealed class RaceDayAuthorizationTests
{
    [Fact]
    public async Task ProtectedPageRedirectsUnauthenticatedVisitorToLogin()
    {
        var filter = CreateFilter(new TestSessions.MemorySession(), "Organiser");

        await filter.Attribute.OnAuthorizationAsync(filter.Context);

        var redirect = Assert.IsType<RedirectToActionResult>(filter.Context.Result);
        Assert.Equal("Login", redirect.ActionName);
        Assert.Equal("Auth", redirect.ControllerName);
        Assert.Equal("/events/manage", redirect.RouteValues!["returnUrl"]);
    }

    [Theory]
    [InlineData("Participant", "Organiser")]
    [InlineData("Organiser", "Participant")]
    public async Task WrongRoleReceivesForbiddenResult(string currentRole, string requiredRole)
    {
        var session = new TestSessions.MemorySession();
        session.SetString("RaceDay.ApiCookie", "private-session");
        session.SetInt32("RaceDay.UserId", 8);
        session.SetString("RaceDay.Role", currentRole);
        var filter = CreateFilter(session, requiredRole);

        await filter.Attribute.OnAuthorizationAsync(filter.Context);

        var forbidden = Assert.IsType<ViewResult>(filter.Context.Result);
        Assert.Equal("~/Views/Auth/AccessDenied.cshtml", forbidden.ViewName);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task MatchingRoleCanContinueToProtectedAction()
    {
        var session = new TestSessions.MemorySession();
        session.SetString("RaceDay.ApiCookie", "private-session");
        session.SetInt32("RaceDay.UserId", 8);
        session.SetString("RaceDay.Role", "Organiser");
        var filter = CreateFilter(session, "Organiser");

        await filter.Attribute.OnAuthorizationAsync(filter.Context);

        Assert.Null(filter.Context.Result);
    }

    private static (RaceDayAuthorizeAttribute Attribute, AuthorizationFilterContext Context) CreateFilter(
        ISession session,
        string role)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<ISessionFeature>(new SessionFeature(session));
        httpContext.Request.Path = "/events/manage";
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return (new RaceDayAuthorizeAttribute(role), new AuthorizationFilterContext(actionContext, []));
    }

    private sealed class SessionFeature(ISession session) : ISessionFeature
    {
        public ISession Session { get; set; } = session;
    }
}
