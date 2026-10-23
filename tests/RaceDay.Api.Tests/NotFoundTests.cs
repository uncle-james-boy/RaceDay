using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Authentication;
using RaceDay.Api.Controllers;
using RaceDay.Api.Data;
using Xunit;

namespace RaceDay.Api.Tests;

public sealed class NotFoundTests
{
    [Fact]
    public async Task EventReadUpdateAndDeleteReturn404ForMissingEvent()
    {
        await using var db = CreateDbContext();
        var controller = new EventsController(db) { ControllerContext = CreateContext(3, UserRoles.Organiser) };

        Assert.IsType<NotFoundResult>((await controller.GetEvent(123, CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>((await controller.UpdateEvent(123, ValidEvent(), CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>(await controller.DeleteEvent(123, CancellationToken.None));
    }

    [Fact]
    public async Task CategoryListUpdateAndDeleteReturn404WhenResourceIsMissing()
    {
        await using var db = CreateDbContext();
        var controller = new CategoriesController(db) { ControllerContext = CreateContext(3, UserRoles.Organiser) };

        Assert.IsType<NotFoundResult>((await controller.GetForEvent(123, CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>((await controller.Update(123, new CategoryWriteRequest
        {
            CategoryName = "Open"
        }, CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>(await controller.Delete(123, CancellationToken.None));
    }

    [Fact]
    public async Task RouteListUpdateAndDeleteReturn404WhenResourceIsMissing()
    {
        await using var db = CreateDbContext();
        var controller = new EventRoutesController(db) { ControllerContext = CreateContext(3, UserRoles.Organiser) };

        Assert.IsType<NotFoundResult>((await controller.GetForEvent(123, CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>((await controller.Update(123, new EventRouteWriteRequest
        {
            RouteName = "Open",
            Distance = 10
        }, CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>(await controller.Delete(123, CancellationToken.None));
    }

    [Fact]
    public async Task EnrolmentCreationAndOrganiserListingReturn404ForMissingEvent()
    {
        await using var db = CreateDbContext();
        var controller = new EnrolmentsController(db) { ControllerContext = CreateContext(3, UserRoles.Participant) };
        var create = await controller.Create(new CreateEnrolmentRequest { EventID = 123, CategoryID = 456 }, CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(create.Result);

        controller.ControllerContext = CreateContext(3, UserRoles.Organiser);
        Assert.IsType<NotFoundResult>((await controller.GetForEvent(123, CancellationToken.None)).Result);
    }

    [Fact]
    public async Task ResultCreationReturns404ForMissingEnrolment()
    {
        await using var db = CreateDbContext();
        var controller = new ResultsController(db) { ControllerContext = CreateContext(3, UserRoles.Organiser) };

        var result = await controller.Create(new CreateRaceResultRequest
        {
            EnrolmentID = 123,
            FinishTime = TimeSpan.FromHours(1),
            Position = 1
        }, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    private static EventWriteRequest ValidEvent() => new()
    {
        Name = "Spring Race",
        EventDate = new DateTime(2027, 3, 1),
        Location = "Cape Town",
        Distance = 21.1m,
        EventType = "Run"
    };

    private static RaceDayDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<RaceDayDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new RaceDayDbContext(options);
    }

    private static ControllerContext CreateContext(int userId, string role)
    {
        var session = new TestSession();
        session.SetInt32(SessionKeys.UserId, userId);
        session.SetString(SessionKeys.Role, role);
        return new ControllerContext { HttpContext = new DefaultHttpContext { Session = session } };
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> values = new();
        public bool IsAvailable => true;
        public string Id { get; } = Guid.NewGuid().ToString();
        public IEnumerable<string> Keys => values.Keys;
        public void Clear() => values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => values.Remove(key);
        public void Set(string key, byte[] value) => values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => values.TryGetValue(key, out value!);
    }
}
