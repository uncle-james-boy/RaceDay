using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using RaceDay.Api.Authentication;
using RaceDay.Api.Controllers;
using RaceDay.Api.Data;
using RaceDay.Api.Data.Entities;
using Xunit;

namespace RaceDay.Api.Tests;

public sealed class ApiSessionFlowTests
{
    [Fact]
    public async Task RealHttpFlowRegistersLogsInEnforcesRolesAndClearsSessionOnLogout()
    {
        await using var app = CreateTestApi();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var anonymous = new HttpClient { BaseAddress = new Uri(address) };

        using var swaggerResponse = await anonymous.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, swaggerResponse.StatusCode);
        using var swagger = JsonDocument.Parse(await swaggerResponse.Content.ReadAsStringAsync());
        var paths = swagger.RootElement.GetProperty("paths");
        Assert.Equal(23, CountOperations(paths));
        Assert.Contains("Creates an event owned by the signed-in Organiser.",
            paths.GetProperty("/api/events").GetProperty("post").GetProperty("summary").GetString());
        Assert.Equal(1, paths.GetProperty("/api/events").GetProperty("post").GetProperty("security").GetArrayLength());
        Assert.False(paths.GetProperty("/api/events").GetProperty("get").TryGetProperty("security", out _));

        var publicEvents = await anonymous.GetAsync("/api/events");
        Assert.Equal(HttpStatusCode.OK, publicEvents.StatusCode);
        var anonymousProfile = await anonymous.GetAsync("/api/users/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousProfile.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync("/api/events", ValidEventBody())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/enrolments")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/results")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/api/auth/logout", null)).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Alex Runner",
            email = "not-an-email",
            password = "secure test password",
            role = "Participant"
        })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Register(anonymous, "invalid-role@example.com", "Administrator")).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await Register(anonymous, "participant@example.com", "Participant")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Register(anonymous, "PARTICIPANT@example.com", "Participant")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Register(anonymous, "organiser@example.com", "Organiser")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Register(anonymous, "other@example.com", "Organiser")).StatusCode);

        var participantCookies = new CookieContainer();
        using var participant = CreateSessionClient(address, participantCookies);
        var participantLogin = await Login(participant, "participant@example.com");
        Assert.Equal(HttpStatusCode.OK, participantLogin.StatusCode);
        var participantSetCookie = string.Join(";", participantLogin.Headers.GetValues("Set-Cookie"));
        Assert.Contains("HttpOnly", participantSetCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SameSite=Lax", participantSetCookie, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(participantCookies.GetCookies(new Uri(address))[".RaceDay.Session"]);
        var participantProfile = await participant.GetAsync("/api/users/profile");
        Assert.Equal(HttpStatusCode.OK, participantProfile.StatusCode);
        Assert.False((await participantProfile.Content.ReadAsStringAsync()).Contains("PasswordHash", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(HttpStatusCode.OK, (await participant.PutAsJsonAsync("/api/users/profile", new
        {
            fullName = "Alex Updated",
            profilePictureUrl = "https://example.com/alex.png"
        })).StatusCode);

        var forbiddenCreate = await participant.PostAsJsonAsync("/api/events", ValidEventBody());
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCreate.StatusCode);

        var organiserCookies = new CookieContainer();
        using var organiser = CreateSessionClient(address, organiserCookies);
        Assert.Equal(HttpStatusCode.OK, (await Login(organiser, "organiser@example.com")).StatusCode);
        Assert.NotNull(organiserCookies.GetCookies(new Uri(address))[".RaceDay.Session"]);
        Assert.Equal(HttpStatusCode.BadRequest, (await organiser.PostAsJsonAsync("/api/events", new
        {
            name = "Invalid Race",
            eventDate = DateTime.Now.AddDays(5),
            location = "Cape Town",
            distance = 0m,
            eventType = "Swim"
        })).StatusCode);
        var createEvent = await organiser.PostAsJsonAsync("/api/events", ValidEventBody(eventDate: DateTime.Now.AddDays(-5)));
        Assert.Equal(HttpStatusCode.Created, createEvent.StatusCode);
        using var eventJson = JsonDocument.Parse(await createEvent.Content.ReadAsStringAsync());
        var eventId = eventJson.RootElement.GetProperty("eventID").GetInt32();

        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/events/{eventId}")).StatusCode);
        var categoryCreate = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", new
        {
            categoryName = "Open",
            minAge = 18,
            maxAge = 65,
            distance = 21.1m
        });
        Assert.Equal(HttpStatusCode.Created, categoryCreate.StatusCode);
        using var categoryJson = JsonDocument.Parse(await categoryCreate.Content.ReadAsStringAsync());
        var categoryId = categoryJson.RootElement.GetProperty("categoryID").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/events/{eventId}/categories")).StatusCode);

        var secondEventResult = await organiser.PostAsJsonAsync("/api/events", ValidEventBody(
            name: "Autumn Race", eventDate: DateTime.Now.AddDays(-3)));
        Assert.Equal(HttpStatusCode.Created, secondEventResult.StatusCode);
        using var secondEventJson = JsonDocument.Parse(await secondEventResult.Content.ReadAsStringAsync());
        var secondEventId = secondEventJson.RootElement.GetProperty("eventID").GetInt32();
        var otherCategoryResult = await organiser.PostAsJsonAsync($"/api/events/{secondEventId}/categories", new
        {
            categoryName = "5 km",
            minAge = (int?)null,
            maxAge = (int?)null,
            distance = 5m
        });
        Assert.Equal(HttpStatusCode.Created, otherCategoryResult.StatusCode);
        using var otherCategoryJson = JsonDocument.Parse(await otherCategoryResult.Content.ReadAsStringAsync());
        var otherCategoryId = otherCategoryJson.RootElement.GetProperty("categoryID").GetInt32();

        var categoryUpdate = await organiser.PutAsJsonAsync($"/api/categories/{categoryId}", new
        {
            categoryName = "Open 21K",
            minAge = 18,
            maxAge = 65,
            distance = 21.1m
        });
        Assert.Equal(HttpStatusCode.OK, categoryUpdate.StatusCode);

        var routeCreate = await organiser.PostAsJsonAsync($"/api/events/{eventId}/routes", new
        {
            routeName = "City Loop",
            distance = 21.1m,
            routeDescription = "Central route",
            routeMapUrl = "https://example.com/map"
        });
        Assert.Equal(HttpStatusCode.Created, routeCreate.StatusCode);
        using var routeJson = JsonDocument.Parse(await routeCreate.Content.ReadAsStringAsync());
        var routeId = routeJson.RootElement.GetProperty("routeID").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/events/{eventId}/routes")).StatusCode);
        var routeUpdate = await organiser.PutAsJsonAsync($"/api/routes/{routeId}", new
        {
            routeName = "City Loop",
            distance = 21.1m,
            routeDescription = "Updated central route",
            routeMapUrl = "https://example.com/map"
        });
        Assert.Equal(HttpStatusCode.OK, routeUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await organiser.DeleteAsync($"/api/routes/{routeId}")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await organiser.PutAsJsonAsync($"/api/events/{eventId}",
            ValidEventBody(name: "Spring Race Updated", eventDate: DateTime.Now.AddDays(-5)))).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await participant.PutAsJsonAsync("/api/users/profile", new
        {
            fullName = "Alex Updated",
            profilePictureUrl = "not-a-url"
        })).StatusCode);

        var mismatch = await participant.PostAsJsonAsync("/api/enrolments", new
        {
            eventID = eventId,
            categoryID = otherCategoryId
        });
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);

        var participantEnrolment = await participant.PostAsJsonAsync("/api/enrolments", new
        {
            eventID = eventId,
            categoryID = categoryId
        });
        Assert.Equal(HttpStatusCode.Created, participantEnrolment.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await participant.PostAsJsonAsync("/api/enrolments", new
        {
            eventID = 0,
            categoryID = categoryId
        })).StatusCode);
        using var enrolmentJson = JsonDocument.Parse(await participantEnrolment.Content.ReadAsStringAsync());
        var enrolmentId = enrolmentJson.RootElement.GetProperty("enrolmentID").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await participant.GetAsync("/api/enrolments")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await organiser.GetAsync($"/api/events/{eventId}/enrolments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await participant.PostAsJsonAsync("/api/results", new
            {
                enrolmentID = enrolmentId,
                finishTime = TimeSpan.FromHours(2),
                position = 4
            })).StatusCode);

        var capturedResult = await organiser.PostAsJsonAsync("/api/results", new
        {
            enrolmentID = enrolmentId,
            finishTime = TimeSpan.FromHours(2),
            position = 4
        });
        Assert.Equal(HttpStatusCode.Created, capturedResult.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await organiser.PostAsJsonAsync("/api/results", new
        {
            enrolmentID = enrolmentId,
            finishTime = TimeSpan.Zero,
            position = 4
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await organiser.PostAsJsonAsync("/api/results", new
        {
            enrolmentID = enrolmentId,
            finishTime = TimeSpan.FromHours(2),
            position = 4
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await participant.GetAsync("/api/results")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await organiser.DeleteAsync($"/api/categories/{categoryId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await organiser.DeleteAsync($"/api/events/{eventId}")).StatusCode);

        using var otherOrganiser = CreateSessionClient(address, new CookieContainer());
        Assert.Equal(HttpStatusCode.OK, (await Login(otherOrganiser, "other@example.com")).StatusCode);
        var notOwner = await otherOrganiser.PutAsJsonAsync($"/api/events/{eventId}", ValidEventBody());
        Assert.Equal(HttpStatusCode.Forbidden, notOwner.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await organiser.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await organiser.GetAsync("/api/users/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await participant.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await participant.GetAsync("/api/enrolments")).StatusCode);
    }

    private static int CountOperations(JsonElement paths)
    {
        var count = 0;
        foreach (var path in paths.EnumerateObject())
        {
            count += path.Value.EnumerateObject().Count();
        }
        return count;
    }

    private static WebApplication CreateTestApi()
    {
        var keyDirectory = Path.Combine(Directory.GetCurrentDirectory(),
            "src", "RaceDay.Api", "obj", "nuget-local", "integration-keyring");
        Directory.CreateDirectory(keyDirectory);
        var databaseName = Guid.NewGuid().ToString();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
            ApplicationName = typeof(Program).Assembly.GetName().Name
        });
        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        builder.Services.AddAuthorization();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new() { Title = "RaceDay API Test", Version = "v1" });
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "RaceDay.Api.xml"));
            options.AddSecurityDefinition("SessionCookie", new Microsoft.OpenApi.OpenApiSecurityScheme
            {
                Type = Microsoft.OpenApi.SecuritySchemeType.ApiKey,
                In = Microsoft.OpenApi.ParameterLocation.Cookie,
                Name = ".RaceDay.Session",
                Description = "Server-side session cookie."
            });
            options.OperationFilter<SessionCookieOperationFilter>();
        });
        builder.Services.AddDbContext<RaceDayDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory))
            .SetApplicationName("RaceDay.Api.Tests");
        builder.Services.AddSession(options =>
        {
            options.Cookie.Name = ".RaceDay.Session";
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
        });

        var app = builder.Build();
        app.UseSwagger();
        app.UseSwaggerUI();
        app.UseSession();
        app.UseAuthorization();
        app.MapControllers();
        return app;
    }

    private static HttpClient CreateSessionClient(string address, CookieContainer cookies) => new(
        new HttpClientHandler { CookieContainer = cookies, UseCookies = true })
    {
        BaseAddress = new Uri(address)
    };

    private static Task<HttpResponseMessage> Register(HttpClient client, string email, string role) =>
        client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Alex Runner",
            email,
            password = "secure test password",
            role
        });

    private static Task<HttpResponseMessage> Login(HttpClient client, string email) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password = "secure test password" });

    private static object ValidEventBody(string name = "Spring Race", DateTime? eventDate = null) => new
    {
        name,
        description = "Road running event",
        eventDate = eventDate ?? DateTime.Now.AddDays(5),
        location = "Cape Town",
        distance = 21.1m,
        eventType = "Run"
    };
}
