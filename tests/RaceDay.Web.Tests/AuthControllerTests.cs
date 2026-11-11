using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Controllers;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Tests;

public sealed class AuthControllerTests
{
    [Fact]
    public async Task RegistrationIsSentToTheApiWithoutAnExistingSessionCookie()
    {
        var handler = new FakeApiHandler((request, _) => Task.FromResult(JsonResponse(HttpStatusCode.Created, "{}")));
        var (session, _) = TestSessions.Create();
        session.SignIn(TestUser("Organiser"), "existing-cookie");
        var controller = CreateController(handler, session);

        var result = await controller.Register(ValidRegistration(), CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AuthController.Login), redirect.ActionName);
        Assert.Equal("api/auth/register", handler.LastPath);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Null(handler.LastApiCookie);
        using var payload = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("Participant", payload.RootElement.GetProperty("role").GetString());
    }

    [Theory]
    [InlineData("Organiser")]
    [InlineData("Participant")]
    public async Task LoginUsesApiAndStoresReturnedRoleAndSessionCookie(string role)
    {
        var handler = new FakeApiHandler((_, _) => Task.FromResult(LoginResponse(role)));
        var (session, _) = TestSessions.Create();
        var controller = CreateController(handler, session);

        var result = await controller.Login(new LoginViewModel
        {
            Email = "person@example.test",
            Password = "correct-password"
        }, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.True(session.IsAuthenticated);
        Assert.Equal(role, session.Role);
        Assert.Equal("api/auth/login", handler.LastPath);
        Assert.Null(handler.LastApiCookie);
    }

    [Fact]
    public async Task IncorrectCredentialsShowApiMessageAndDoNotSignIn()
    {
        var handler = new FakeApiHandler((_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.Unauthorized,
            "{\"message\":\"Email or password is incorrect.\"}")));
        var (session, _) = TestSessions.Create();
        var controller = CreateController(handler, session);

        var result = await controller.Login(new LoginViewModel
        {
            Email = "person@example.test",
            Password = "wrong-password"
        }, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.False(session.IsAuthenticated);
        Assert.Contains(controller.ModelState[string.Empty]!.Errors,
            error => error.ErrorMessage == "Email or password is incorrect.");
    }

    [Fact]
    public async Task DuplicateRegistrationShowsTheConflictMessageFromApi()
    {
        var handler = new FakeApiHandler((_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.Conflict,
            "{\"message\":\"An account with this email already exists.\"}")));
        var (session, _) = TestSessions.Create();
        var controller = CreateController(handler, session);

        var result = await controller.Register(ValidRegistration(), CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Contains(controller.ModelState[string.Empty]!.Errors,
            error => error.ErrorMessage == "An account with this email already exists.");
    }

    [Fact]
    public async Task LogoutCallsApiWithCurrentCookieThenClearsMvcSession()
    {
        var handler = new FakeApiHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)));
        var (session, _) = TestSessions.Create();
        session.SignIn(TestUser("Participant"), "the-current-cookie");
        var controller = CreateController(handler, session);

        var result = await controller.Logout(CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("api/auth/logout", handler.LastPath);
        Assert.Equal("the-current-cookie", handler.LastApiCookie);
        Assert.False(session.IsAuthenticated);
        Assert.Null(session.Role);
    }

    [Fact]
    public async Task RegistrationRejectsUnknownRolesBeforeCallingApi()
    {
        var handler = new FakeApiHandler((_, _) => throw new InvalidOperationException("The API should not be called."));
        var (session, _) = TestSessions.Create();
        var controller = CreateController(handler, session);
        var model = ValidRegistration();
        model.Role = "Administrator";

        var result = await controller.Register(model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Contains(controller.ModelState[nameof(RegisterViewModel.Role)]!.Errors,
            error => error.ErrorMessage == "Choose Organiser or Participant.");
    }

    [Fact]
    public void UnsafeAuthenticationActionsRequireAntiforgeryValidation()
    {
        Assert.NotNull(typeof(AuthController).GetMethod(nameof(AuthController.Register), [typeof(RegisterViewModel), typeof(CancellationToken)])!
            .GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true).SingleOrDefault());
        Assert.NotNull(typeof(AuthController).GetMethod(nameof(AuthController.Login), [typeof(LoginViewModel), typeof(CancellationToken)])!
            .GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true).SingleOrDefault());
        Assert.NotNull(typeof(AuthController).GetMethod(nameof(AuthController.Logout), [typeof(CancellationToken)])!
            .GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true).SingleOrDefault());
    }

    private static AuthController CreateController(FakeApiHandler handler, ApiSessionContext session)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:7107/") };
        return new AuthController(new RaceDayApiClient(httpClient, session), session)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static RegisterViewModel ValidRegistration() => new()
    {
        FullName = "Demo Runner",
        Email = "runner@example.test",
        Password = "correct-password",
        Role = "Participant"
    };

    private static ApiUserResponse TestUser(string role) =>
        new(22, "Demo User", "person@example.test", role, null);

    private static HttpResponseMessage LoginResponse(string role)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(TestUser(role))
        };
        response.Headers.TryAddWithoutValidation(
            "Set-Cookie",
            $"{ApiSessionContext.ApiSessionCookieName}=session-{role.ToLowerInvariant()}; path=/; httponly; samesite=lax");
        return response;
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };

    private sealed class FakeApiHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        : HttpMessageHandler
    {
        public string? LastPath { get; private set; }
        public HttpMethod? LastMethod { get; private set; }
        public string? LastBody { get; private set; }
        public string? LastApiCookie { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastPath = request.RequestUri!.AbsolutePath.TrimStart('/');
            LastMethod = request.Method;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            LastApiCookie = request.Headers.TryGetValues("Cookie", out var cookies)
                ? cookies.Single().Split('=', 2)[1]
                : null;
            return await responseFactory(request, cancellationToken);
        }
    }
}
