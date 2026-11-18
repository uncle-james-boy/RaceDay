using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using RaceDay.Web.Controllers;
using RaceDay.Web.Models;
using RaceDay.Web.Security;
using RaceDay.Web.Services;

namespace RaceDay.Web.Tests;

public sealed class ParticipantControllerTests
{
    [Fact]
    public async Task UpcomingEventsUseApiSupportedSearchAndDateFilters()
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, "[]")));
        var controller = CreateController(handler);

        var result = await controller.Index("Cape Town", "Run", new DateOnly(2027, 3, 1), null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Null(view.ViewName);
        Assert.Contains("search=Cape%20Town", handler.LastUri);
        Assert.Contains("eventType=Run", handler.LastUri);
        Assert.Contains("fromDate=2027-03-01", handler.LastUri);
        Assert.Equal("session-participant", handler.LastApiCookie);
    }

    [Fact]
    public async Task EventDetailsLoadsEventCategoriesAndRoutesThroughApi()
    {
        var handler = new RecordingHandler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath switch
        {
            "/api/events/5" => JsonResponse(HttpStatusCode.OK,
                """{"eventID":5,"name":"Harbour Run","description":"Road race","eventDate":"2027-03-01T08:00:00","location":"Cape Town","distance":10,"eventType":"Run","eventBannerUrl":"https://example.test/banner.jpg"}"""),
            "/api/events/5/categories" => JsonResponse(HttpStatusCode.OK,
                """[{"categoryID":2,"eventID":5,"categoryName":"Open","minAge":null,"maxAge":null,"distance":10}]"""),
            "/api/events/5/routes" => JsonResponse(HttpStatusCode.OK,
                """[{"routeID":3,"eventID":5,"routeName":"10 km loop","distance":10,"routeDescription":"Coastal course","routeMapUrl":null}]"""),
            _ => JsonResponse(HttpStatusCode.NotFound, "{}")
        }));
        var controller = CreateController(handler);

        var result = await controller.Details(5, CancellationToken.None);

        var model = Assert.IsType<EventDetailsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("Harbour Run", model.Event!.Name);
        Assert.Equal("https://example.test/banner.jpg", model.Event.EventBannerUrl);
        Assert.Equal("Open", Assert.Single(model.Categories).CategoryName);
        Assert.Equal("10 km loop", Assert.Single(model.Routes).RouteName);
        Assert.Equal("/api/events/5/routes", handler.LastPath);
    }

    [Fact]
    public async Task EnrolmentPostsOnlyEventAndCategoryAndUsesCurrentSessionCookie()
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.Created,
            """{"enrolmentID":9,"eventID":5,"eventName":"Harbour Run","categoryID":2,"categoryName":"Open","participantID":44,"participantName":"Demo Runner","enrolmentDate":"2026-11-11T08:00:00Z","status":"Pending"}""")));
        var controller = CreateController(handler);

        var result = await controller.Enrol(new EnrolParticipantModel { EventID = 5, CategoryID = 2 }, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ParticipantController.MyEvents), redirect.ActionName);
        Assert.Equal("/api/enrolments", handler.LastPath);
        Assert.Equal("session-participant", handler.LastApiCookie);
        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal(5, body.RootElement.GetProperty("eventID").GetInt32());
        Assert.Equal(2, body.RootElement.GetProperty("categoryID").GetInt32());
        Assert.False(body.RootElement.TryGetProperty("participantID", out _));
    }

    [Fact]
    public async Task EnrolmentApiValidationErrorIsShownAsAFriendlyMessage()
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.BadRequest,
            "{\"message\":\"The selected category does not belong to the selected event.\"}")));
        var controller = CreateController(handler);

        var result = await controller.Enrol(new EnrolParticipantModel { EventID = 5, CategoryID = 99 }, CancellationToken.None);

        Assert.Equal(nameof(ParticipantController.Details), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal("The selected category does not belong to the selected event.", controller.TempData["ParticipantError"]);
    }

    [Theory]
    [InlineData("/api/enrolments")]
    [InlineData("/api/results")]
    public async Task PersonalListsUseSessionScopedApiRoutesWithoutParticipantId(string expectedPath)
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, "[]")));
        var controller = CreateController(handler);

        if (expectedPath == "/api/enrolments") await controller.MyEvents(CancellationToken.None);
        else await controller.MyResults(CancellationToken.None);

        Assert.Equal(expectedPath, handler.LastPath);
        Assert.DoesNotContain("participant", handler.LastUri, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("session-participant", handler.LastApiCookie);
    }

    [Fact]
    public async Task MyEventsDisplaysEnrolmentDetailsAndStatusFromApi()
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK,
            """[{"enrolmentID":9,"eventID":5,"eventName":"Harbour Run","categoryID":2,"categoryName":"Open","participantID":44,"participantName":"Demo Runner","enrolmentDate":"2026-11-11T08:00:00Z","status":"Pending"}]""")));
        var controller = CreateController(handler);

        var result = await controller.MyEvents(CancellationToken.None);

        var model = Assert.IsType<ParticipantListViewModel<ParticipantEnrolment>>(Assert.IsType<ViewResult>(result).Model);
        var enrolment = Assert.Single(model.Items);
        Assert.Equal("Harbour Run", enrolment.EventName);
        Assert.Equal("Open", enrolment.CategoryName);
        Assert.Equal("Pending", enrolment.Status);
        Assert.Equal("/api/enrolments", handler.LastPath);
    }

    [Fact]
    public async Task MyResultsDisplaysPersonalRaceHistoryFromApi()
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK,
            """[{"resultID":6,"enrolmentID":9,"eventID":5,"eventName":"Harbour Run","eventDate":"2026-08-02T08:00:00","categoryName":"Open","finishTime":"00:42:30","position":4,"capturedAt":"2026-08-02T12:00:00Z"}]""")));
        var controller = CreateController(handler);

        var result = await controller.MyResults(CancellationToken.None);

        var model = Assert.IsType<ParticipantListViewModel<ParticipantRaceResult>>(Assert.IsType<ViewResult>(result).Model);
        var raceResult = Assert.Single(model.Items);
        Assert.Equal("Harbour Run", raceResult.EventName);
        Assert.Equal(new DateOnly(2026, 8, 2), DateOnly.FromDateTime(raceResult.EventDate));
        Assert.Equal("Open", raceResult.CategoryName);
        Assert.Equal(TimeSpan.FromMinutes(42.5), raceResult.FinishTime);
        Assert.Equal(4, raceResult.Position);
    }

    [Fact]
    public async Task ApiUnavailableProducesAFriendlyPageError()
    {
        var handler = new RecordingHandler((_, _) => throw new HttpRequestException("private connection details"));
        var controller = CreateController(handler);

        var result = await controller.MyEvents(CancellationToken.None);

        var model = Assert.IsType<ParticipantListViewModel<ParticipantEnrolment>>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("The RaceDay API could not be reached. Check that it is running and try again.", model.ErrorMessage);
        Assert.DoesNotContain("private connection details", model.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProfileLoadsAndUpdatesOnlyTheCurrentApiSessionProfile()
    {
        var handler = new RecordingHandler((request, _) => Task.FromResult(request.Method == HttpMethod.Put
            ? JsonResponse(HttpStatusCode.OK,
                """{"userID":44,"fullName":"Updated Runner","email":"runner@example.test","role":"Participant","hasProfilePicture":true,"createdAt":"2026-01-01T00:00:00Z"}""")
            : request.Method == HttpMethod.Post
                ? JsonResponse(HttpStatusCode.OK, """{"hasProfilePicture":true}""")
                : JsonResponse(HttpStatusCode.OK,
                    """{"userID":44,"fullName":"Demo Runner","email":"runner@example.test","role":"Participant","hasProfilePicture":false,"createdAt":"2026-01-01T00:00:00Z"}""")));
        var controller = CreateController(handler);

        var getResult = await controller.Profile(CancellationToken.None);
        var page = Assert.IsType<ParticipantProfileViewModel>(Assert.IsType<ViewResult>(getResult).Model);
        Assert.Equal("runner@example.test", page.Profile!.Email);
        Assert.Equal("Demo Runner", page.Edit.FullName);
        Assert.False(page.Profile.HasProfilePicture);

        using var imageStream = new MemoryStream([1, 2, 3]);
        var file = new FormFile(imageStream, 0, imageStream.Length, "Edit.ProfilePictureFile", "profile.png")
        {
            Headers = new HeaderDictionary { ["Content-Type"] = "image/png" }
        };

        var updateResult = await controller.UpdateProfile(new UpdateParticipantProfileModel
        {
            FullName = "Updated Runner",
            ProfilePictureFile = file
        }, CancellationToken.None);

        Assert.Equal(nameof(ParticipantController.Profile), Assert.IsType<RedirectToActionResult>(updateResult).ActionName);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal("/api/users/profile/picture", handler.LastPath);
        Assert.Equal("session-participant", handler.LastApiCookie);
        Assert.Equal("multipart/form-data", handler.LastContentType);
        Assert.True(handler.LastBody!.Contains("name=\"file\"", StringComparison.Ordinal)
            || handler.LastBody.Contains("name=file", StringComparison.Ordinal));
        Assert.Contains("profile.png", handler.LastBody);
        Assert.Contains("/api/users/profile", handler.RequestPaths);
    }

    [Fact]
    public async Task ProfilePictureIsProxiedThroughTheAuthenticatedApiSession()
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([1, 2, 3])
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg") }
            }
        }));
        var controller = CreateController(handler);

        var result = await controller.ProfilePicture(CancellationToken.None);

        var image = Assert.IsType<FileContentResult>(result);
        Assert.Equal("image/jpeg", image.ContentType);
        Assert.Equal(new byte[] { 1, 2, 3 }, image.FileContents);
        Assert.Equal("/api/users/profile/picture", handler.LastPath);
        Assert.Equal("session-participant", handler.LastApiCookie);
        Assert.Equal("no-store, private", controller.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task ProfilePictureMissingInApiReturnsNotFound()
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.NotFound,
            """{"message":"No profile picture."}""")));
        var controller = CreateController(handler);

        var result = await controller.ProfilePicture(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ProfilePictureUploadFailureIsShownAfterProfileDetailsSave()
    {
        var handler = new RecordingHandler((request, _) => Task.FromResult(request.Method == HttpMethod.Put
            ? JsonResponse(HttpStatusCode.OK,
                """{"userID":44,"fullName":"Updated Runner","email":"runner@example.test","role":"Participant","hasProfilePicture":false,"createdAt":"2026-01-01T00:00:00Z"}""")
            : JsonResponse(HttpStatusCode.BadGateway, """{"message":"Storage is unavailable."}""")));
        var controller = CreateController(handler);
        using var imageStream = new MemoryStream([1, 2, 3]);
        var file = new FormFile(imageStream, 0, imageStream.Length, "Edit.ProfilePictureFile", "profile.png")
        {
            Headers = new HeaderDictionary { ["Content-Type"] = "image/png" }
        };

        var result = await controller.UpdateProfile(new UpdateParticipantProfileModel
        {
            FullName = "Updated Runner", ProfilePictureFile = file
        }, CancellationToken.None);

        Assert.Equal(nameof(ParticipantController.Profile), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Contains("picture upload failed", Assert.IsType<string>(controller.TempData["ParticipantError"]), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("/api/users/profile/picture", handler.LastPath);
    }

    [Fact]
    public async Task ProfilePictureUploadOverTwoMegabytesIsRejectedBeforeApiCalls()
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, "{}")));
        var controller = CreateController(handler);
        using var stream = new MemoryStream([1]);
        var file = new FormFile(stream, 0, ParticipantController.MaximumProfilePictureSizeBytes + 1,
            "Edit.ProfilePictureFile", "large.png");

        var result = await controller.UpdateProfile(new UpdateParticipantProfileModel
        {
            FullName = "Runner", ProfilePictureFile = file
        }, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal("/api/users/profile", Assert.Single(handler.RequestPaths));
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Contains("2 MB", Assert.Single(controller.ModelState["Edit.ProfilePictureFile"]!.Errors).ErrorMessage);
    }

    [Fact]
    public void EveryParticipantPageUsesParticipantRoleAuthorization()
    {
        var attribute = System.Reflection.CustomAttributeData
            .GetCustomAttributes(typeof(ParticipantController))
            .Single(item => item.AttributeType == typeof(RaceDayAuthorizeAttribute));
        Assert.Equal("Participant", attribute.ConstructorArguments.Single().Value);
    }

    private static ParticipantController CreateController(RecordingHandler handler)
    {
        var (session, _) = TestSessions.Create();
        session.SignIn(new ApiUserResponse(44, "Demo Runner", "runner@example.test", "Participant", false), "session-participant");
        var context = new DefaultHttpContext();
        var controller = new ParticipantController(new RaceDayApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://localhost:7107/") }, session))
        {
            ControllerContext = new ControllerContext { HttpContext = context },
            TempData = new TempDataDictionary(context, new NoOpTempDataProvider())
        };
        return controller;
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };

    private sealed class RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        public string? LastPath { get; private set; }
        public string? LastUri { get; private set; }
        public string? LastBody { get; private set; }
        public string? LastApiCookie { get; private set; }
        public HttpMethod? LastMethod { get; private set; }
        public string? LastContentType { get; private set; }
        public List<string> RequestPaths { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastPath = request.RequestUri!.AbsolutePath;
            RequestPaths.Add(LastPath);
            LastUri = request.RequestUri.PathAndQuery;
            LastMethod = request.Method;
            LastContentType = request.Content?.Headers.ContentType?.MediaType;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            LastApiCookie = request.Headers.TryGetValues("Cookie", out var cookies)
                ? cookies.Single().Split('=', 2)[1]
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
