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

        var publicEvents = await anonymous.GetAsync("/api/events");
        Assert.Equal(HttpStatusCode.OK, publicEvents.StatusCode);
        var anonymousProfile = await anonymous.GetAsync("/api/users/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousProfile.StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await Register(anonymous, "participant@example.com", "Participant")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Register(anonymous, "organiser@example.com", "Organiser")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Register(anonymous, "other@example.com", "Organiser")).StatusCode);

        var participantCookies = new CookieContainer();
        using var participant = CreateSessionClient(address, participantCookies);
        Assert.Equal(HttpStatusCode.OK, (await Login(participant, "participant@example.com")).StatusCode);
        Assert.NotNull(participantCookies.GetCookies(new Uri(address))[".RaceDay.Session.Test"]);
        var participantProfile = await participant.GetAsync("/api/users/profile");
        Assert.Equal(HttpStatusCode.OK, participantProfile.StatusCode);
        Assert.False((await participantProfile.Content.ReadAsStringAsync()).Contains("PasswordHash", StringComparison.OrdinalIgnoreCase));

        var forbiddenCreate = await participant.PostAsJsonAsync("/api/events", ValidEventBody());
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCreate.StatusCode);

        var organiserCookies = new CookieContainer();
        using var organiser = CreateSessionClient(address, organiserCookies);
        Assert.Equal(HttpStatusCode.OK, (await Login(organiser, "organiser@example.com")).StatusCode);
        Assert.NotNull(organiserCookies.GetCookies(new Uri(address))[".RaceDay.Session.Test"]);
        var createEvent = await organiser.PostAsJsonAsync("/api/events", ValidEventBody());
        Assert.Equal(HttpStatusCode.Created, createEvent.StatusCode);
        using var eventJson = JsonDocument.Parse(await createEvent.Content.ReadAsStringAsync());
        var eventId = eventJson.RootElement.GetProperty("eventID").GetInt32();

        using var otherOrganiser = CreateSessionClient(address, new CookieContainer());
        Assert.Equal(HttpStatusCode.OK, (await Login(otherOrganiser, "other@example.com")).StatusCode);
        var notOwner = await otherOrganiser.PutAsJsonAsync($"/api/events/{eventId}", ValidEventBody());
        Assert.Equal(HttpStatusCode.Forbidden, notOwner.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await organiser.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await organiser.GetAsync("/api/users/profile")).StatusCode);

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
            options.SwaggerDoc("v1", new() { Title = "RaceDay API Test", Version = "v1" }));
        builder.Services.AddDbContext<RaceDayDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory))
            .SetApplicationName("RaceDay.Api.Tests");
        builder.Services.AddSession(options =>
        {
            options.Cookie.Name = ".RaceDay.Session.Test";
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

    private static object ValidEventBody() => new
    {
        name = "Spring Race",
        description = "Road running event",
        eventDate = DateTime.Now.AddDays(5),
        location = "Cape Town",
        distance = 21.1m,
        eventType = "Run"
    };
}
