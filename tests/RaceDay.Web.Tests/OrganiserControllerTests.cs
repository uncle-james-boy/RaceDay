using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using RaceDay.Web.Controllers;
using RaceDay.Web.Models;
using RaceDay.Web.Security;
using RaceDay.Web.Services;

namespace RaceDay.Web.Tests;

public sealed class OrganiserControllerTests
{
    private const string OwnEvent = """{"eventID":5,"name":"Harbour Run","description":"Road race","eventDate":"2027-03-01T08:00:00","location":"Cape Town","distance":10,"eventType":"Run","eventBannerUrl":null,"enrolmentCount":2}""";
    private const string Event = """{"eventID":5,"name":"Harbour Run","description":"Road race","eventDate":"2027-03-01T08:00:00","location":"Cape Town","distance":10,"eventType":"Run","eventBannerUrl":null}""";

    [Fact]
    public async Task DashboardLoadsOnlyEventsReturnedByOwnerScopedApi()
    {
        var handler = new FakeHandler((request, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, $"[{OwnEvent}]")));
        var controller = CreateController(handler);

        var result = await controller.Index(CancellationToken.None);

        var model = Assert.IsType<OrganiserDashboardViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(5, Assert.Single(model.Events).EventID);
        Assert.Equal(2, model.Events[0].EnrolmentCount);
        Assert.Equal("/api/events/mine", handler.LastPath);
        Assert.Equal("session-organiser", handler.LastCookie);
    }

    [Fact]
    public async Task CreateEventUsesAuthenticatedApiSessionAndDoesNotSendOwnerId()
    {
        var handler = new FakeHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.Created, Event)));
        var controller = CreateController(handler);

        var result = await controller.CreateEvent(ValidEvent(), CancellationToken.None);

        Assert.Equal(nameof(OrganiserController.Index), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal("/api/events", handler.LastPath);
        Assert.Equal("session-organiser", handler.LastCookie);
        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.False(body.RootElement.TryGetProperty("organiserID", out _));
    }

    [Fact]
    public async Task EditEventChecksOwnedEventListBeforeSendingUpdate()
    {
        var handler = new FakeHandler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath == "/api/events/mine"
            ? JsonResponse(HttpStatusCode.OK, $"[{OwnEvent}]")
            : JsonResponse(HttpStatusCode.OK, Event)));
        var controller = CreateController(handler);

        var result = await controller.EditEvent(5, ValidEvent(), CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(HttpMethod.Put, handler.LastMethod);
        Assert.Equal("/api/events/5", handler.LastPath);
        Assert.Equal("session-organiser", handler.LastCookie);
    }

    [Fact]
    public async Task EditEventRejectsEventNotPresentInCurrentOwnersList()
    {
        var handler = new FakeHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, "[]")));
        var controller = CreateController(handler);

        var result = await controller.EditEvent(99, ValidEvent(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal("/api/events/mine", handler.LastPath);
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
    }

    [Fact]
    public async Task CategoryPageVerifiesOwnershipBeforeLoadingCategories()
    {
        var handler = new FakeHandler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath == "/api/events/mine"
            ? JsonResponse(HttpStatusCode.OK, $"[{OwnEvent}]")
            : JsonResponse(HttpStatusCode.OK,
                """[{"categoryID":3,"eventID":5,"categoryName":"Open","minAge":null,"maxAge":null,"distance":10}]""")));
        var controller = CreateController(handler);

        var result = await controller.Categories(5, CancellationToken.None);

        var model = Assert.IsType<OrganiserCategoriesViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("Harbour Run", model.EventName);
        Assert.Equal("Open", Assert.Single(model.Categories).CategoryName);
        Assert.Equal("/api/events/5/categories", handler.LastPath);
    }

    [Fact]
    public async Task CategoryWriteVerifiesOwnerAndCallsCreateEndpoint()
    {
        var handler = new FakeHandler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath == "/api/events/mine"
            ? JsonResponse(HttpStatusCode.OK, $"[{OwnEvent}]")
            : JsonResponse(HttpStatusCode.Created,
                """{"categoryID":3,"eventID":5,"categoryName":"Open","minAge":null,"maxAge":null,"distance":10}""")));
        var controller = CreateController(handler);

        var result = await controller.CreateCategory(5, new CategoryEditModel
        {
            CategoryName = "Open", Distance = 10
        }, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal("/api/events/5/categories", handler.LastPath);
        Assert.Equal("session-organiser", handler.LastCookie);
    }

    [Fact]
    public async Task RosterRejectsEventNotOwnedByCurrentOrganiser()
    {
        var handler = new FakeHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, "[]")));
        var controller = CreateController(handler);

        var result = await controller.Enrolments(500, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal("/api/events/mine", handler.LastPath);
    }

    [Fact]
    public async Task ResultsPageLoadsRosterAndPublishedResultsForOwnedEvent()
    {
        var handler = new FakeHandler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath switch
        {
            "/api/events/mine" => JsonResponse(HttpStatusCode.OK, $"[{OwnEvent}]"),
            "/api/events/5/enrolments" => JsonResponse(HttpStatusCode.OK,
                """[{"enrolmentID":8,"eventID":5,"eventName":"Harbour Run","categoryID":3,"categoryName":"Open","participantID":41,"participantName":"Runner One","enrolmentDate":"2026-11-01T08:00:00Z","status":"Confirmed"}]"""),
            "/api/events/5/results" => JsonResponse(HttpStatusCode.OK,
                """[{"resultID":2,"enrolmentID":8,"eventID":5,"eventName":"Harbour Run","eventDate":"2026-11-01T08:00:00","categoryName":"Open","finishTime":"00:42:30","position":1,"capturedAt":"2026-11-01T12:00:00Z"}]"""),
            _ => JsonResponse(HttpStatusCode.NotFound, "{}")
        }));
        var controller = CreateController(handler);

        var result = await controller.Results(5, CancellationToken.None);

        var model = Assert.IsType<OrganiserRosterViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("Runner One", Assert.Single(model.Enrolments).ParticipantName);
        Assert.Equal(1, Assert.Single(model.Results).Position);
        Assert.Equal("/api/events/5/results", handler.LastPath);
    }

    [Fact]
    public async Task ResultCreationValidatesTimeAndPostsParticipantEntryToApi()
    {
        var handler = new FakeHandler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath switch
        {
            "/api/events/mine" => JsonResponse(HttpStatusCode.OK, $"[{OwnEvent}]"),
            "/api/events/5/enrolments" => JsonResponse(HttpStatusCode.OK,
                """[{"enrolmentID":8,"eventID":5,"eventName":"Harbour Run","categoryID":3,"categoryName":"Open","participantID":41,"participantName":"Runner One","enrolmentDate":"2026-11-01T08:00:00Z","status":"Confirmed"}]"""),
            "/api/events/5/results" => JsonResponse(HttpStatusCode.OK, "[]"),
            "/api/results" => JsonResponse(HttpStatusCode.Created,
                """{"resultID":2,"enrolmentID":8,"eventID":5,"eventName":"Harbour Run","eventDate":"2026-11-01T00:00:00","categoryName":"Open","finishTime":"00:42:30","position":1,"capturedAt":"2026-11-01T12:00:00Z"}"""),
            _ => JsonResponse(HttpStatusCode.NotFound, "{}")
        }));
        var controller = CreateController(handler);

        var result = await controller.CreateResult(5, new CreateResultModel
        {
            EnrolmentID = 8, FinishTime = "00:42:30", Position = 1
        }, CancellationToken.None);

        Assert.Equal(nameof(OrganiserController.Results), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal("/api/results", handler.LastPath);
        Assert.Equal("session-organiser", handler.LastCookie);
        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal(8, body.RootElement.GetProperty("enrolmentID").GetInt32());
        Assert.False(body.RootElement.TryGetProperty("participantID", out _));
    }

    [Fact]
    public void EveryOrganiserPageRequiresOrganiserRole()
    {
        var attribute = System.Reflection.CustomAttributeData
            .GetCustomAttributes(typeof(OrganiserController))
            .Single(item => item.AttributeType == typeof(RaceDayAuthorizeAttribute));
        Assert.Equal("Organiser", attribute.ConstructorArguments.Single().Value);
    }

    private static EventEditModel ValidEvent() => new()
    {
        Name = "Harbour Run", Description = "Road race", EventDate = new DateTime(2027, 3, 1),
        Location = "Cape Town", Distance = 10, EventType = "Run"
    };

    private static OrganiserController CreateController(FakeHandler handler)
    {
        var (session, _) = TestSessions.Create();
        session.SignIn(new ApiUserResponse(7, "Demo Organiser", "organiser@example.test", "Organiser", null), "session-organiser");
        var context = new DefaultHttpContext();
        return new OrganiserController(new RaceDayApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://localhost:7107/") }, session))
        {
            ControllerContext = new ControllerContext { HttpContext = context },
            TempData = new TempDataDictionary(context, new NoOpTempDataProvider())
        };
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };

    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        public string? LastPath { get; private set; }
        public HttpMethod? LastMethod { get; private set; }
        public string? LastBody { get; private set; }
        public string? LastCookie { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastPath = request.RequestUri!.AbsolutePath;
            LastMethod = request.Method;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            LastCookie = request.Headers.TryGetValues("Cookie", out var values)
                ? values.Single().Split('=', 2)[1]
                : null;
            return await respond(request, cancellationToken);
        }
    }

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
