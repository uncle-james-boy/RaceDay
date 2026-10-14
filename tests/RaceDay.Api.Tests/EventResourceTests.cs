using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Authentication;
using RaceDay.Api.Controllers;
using RaceDay.Api.Data;
using RaceDay.Api.Data.Entities;
using Xunit;

namespace RaceDay.Api.Tests;

public sealed class EventResourceTests
{
    [Fact]
    public async Task CategoryCreateBindsItToTheOwnedEvent()
    {
        await using var db = CreateDbContext();
        var raceEvent = await AddEvent(db, organiserId: 5);
        var controller = CreateCategoriesController(db, userId: 5);

        var result = await controller.Create(raceEvent.EventID, new CategoryWriteRequest
        {
            CategoryName = "Open",
            MinAge = 18,
            Distance = 10
        }, CancellationToken.None);

        Assert.IsType<CreatedResult>(result.Result);
        var category = await db.Categories.SingleAsync();
        Assert.Equal(raceEvent.EventID, category.EventID);
        Assert.Equal("Open", category.CategoryName);
    }

    [Fact]
    public async Task CategoryDeleteRejectsAnotherOrganisersEvent()
    {
        await using var db = CreateDbContext();
        var raceEvent = await AddEvent(db, organiserId: 9);
        var category = new Category { EventID = raceEvent.EventID, CategoryName = "Open" };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        var controller = CreateCategoriesController(db, userId: 5);

        var result = await controller.Delete(category.CategoryID, CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result).StatusCode);
        Assert.Single(await db.Categories.ToListAsync());
    }

    [Fact]
    public async Task CategoryDeleteReturnsConflictForAnEnrolledCategory()
    {
        await using var db = CreateDbContext();
        var raceEvent = await AddEvent(db, organiserId: 5);
        var category = new Category { EventID = raceEvent.EventID, CategoryName = "Open" };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        db.Enrolments.Add(new Enrolment
        {
            EventID = raceEvent.EventID,
            CategoryID = category.CategoryID,
            ParticipantID = 20,
            Status = "Pending"
        });
        await db.SaveChangesAsync();
        var controller = CreateCategoriesController(db, userId: 5);

        var result = await controller.Delete(category.CategoryID, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task RouteCreateRejectsDuplicateNamesForOneEvent()
    {
        await using var db = CreateDbContext();
        var raceEvent = await AddEvent(db, organiserId: 5);
        db.EventRoutes.Add(new EventRoute
        {
            EventID = raceEvent.EventID,
            RouteName = "10 km",
            Distance = 10
        });
        await db.SaveChangesAsync();
        var controller = CreateRoutesController(db, userId: 5);

        var result = await controller.Create(raceEvent.EventID, new EventRouteWriteRequest
        {
            RouteName = " 10 km ",
            Distance = 10
        }, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Single(await db.EventRoutes.ToListAsync());
    }

    [Fact]
    public async Task RouteUpdateAndDeleteRequireEventOwnership()
    {
        await using var db = CreateDbContext();
        var raceEvent = await AddEvent(db, organiserId: 9);
        var route = new EventRoute
        {
            EventID = raceEvent.EventID,
            RouteName = "10 km",
            Distance = 10
        };
        db.EventRoutes.Add(route);
        await db.SaveChangesAsync();
        var controller = CreateRoutesController(db, userId: 5);

        var update = await controller.Update(route.RouteID, new EventRouteWriteRequest
        {
            RouteName = "Changed",
            Distance = 12
        }, CancellationToken.None);
        var delete = await controller.Delete(route.RouteID, CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(update.Result).StatusCode);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(delete).StatusCode);
        Assert.Equal("10 km", (await db.EventRoutes.SingleAsync()).RouteName);
    }

    [Fact]
    public async Task PublicListsReturnOnlyResourcesForTheRequestedEvent()
    {
        await using var db = CreateDbContext();
        var first = await AddEvent(db, organiserId: 5);
        var second = await AddEvent(db, organiserId: 5, name: "Autumn Race");
        db.Categories.AddRange(
            new Category { EventID = first.EventID, CategoryName = "Open" },
            new Category { EventID = second.EventID, CategoryName = "Junior" });
        db.EventRoutes.AddRange(
            new EventRoute { EventID = first.EventID, RouteName = "10 km", Distance = 10 },
            new EventRoute { EventID = second.EventID, RouteName = "5 km", Distance = 5 });
        await db.SaveChangesAsync();

        var categoryAction = await CreateCategoriesController(db, 5).GetForEvent(first.EventID, CancellationToken.None);
        var routeAction = await CreateRoutesController(db, 5).GetForEvent(first.EventID, CancellationToken.None);

        var categories = Assert.IsType<List<CategoryResponse>>(Assert.IsType<OkObjectResult>(categoryAction.Result).Value);
        var routes = Assert.IsType<List<EventRouteResponse>>(Assert.IsType<OkObjectResult>(routeAction.Result).Value);
        Assert.Single(categories);
        Assert.Equal("Open", categories[0].CategoryName);
        Assert.Single(routes);
        Assert.Equal("10 km", routes[0].RouteName);
    }

    private static async Task<RaceEvent> AddEvent(RaceDayDbContext db, int organiserId, string name = "Spring Race")
    {
        var raceEvent = new RaceEvent
        {
            OrganiserID = organiserId,
            Name = name,
            EventDate = new DateTime(2027, 3, 1),
            Location = "Cape Town",
            Distance = 21.1m,
            EventType = "Run"
        };
        db.Events.Add(raceEvent);
        await db.SaveChangesAsync();
        return raceEvent;
    }

    private static RaceDayDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<RaceDayDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new RaceDayDbContext(options);
    }

    private static CategoriesController CreateCategoriesController(RaceDayDbContext db, int userId) =>
        new(db) { ControllerContext = new ControllerContext { HttpContext = CreateHttpContext(userId) } };

    private static EventRoutesController CreateRoutesController(RaceDayDbContext db, int userId) =>
        new(db) { ControllerContext = new ControllerContext { HttpContext = CreateHttpContext(userId) } };

    private static DefaultHttpContext CreateHttpContext(int userId)
    {
        var session = new TestSession();
        session.SetInt32(SessionKeys.UserId, userId);
        session.SetString(SessionKeys.Role, UserRoles.Organiser);
        return new DefaultHttpContext { Session = session };
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
