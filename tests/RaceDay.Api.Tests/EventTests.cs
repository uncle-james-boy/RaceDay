using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Authentication;
using RaceDay.Api.Controllers;
using RaceDay.Api.Data;
using RaceDay.Api.Data.Entities;
using Xunit;

namespace RaceDay.Api.Tests;

public sealed class EventTests
{
    [Fact]
    public async Task CreateEventUsesTheOrganiserFromTheSession()
    {
        await using var db = CreateDbContext();
        var controller = CreateController(db, userId: 7, UserRoles.Organiser);

        var result = await controller.CreateEvent(ValidRequest(), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var response = Assert.IsType<EventResponse>(created.Value);
        var saved = await db.Events.SingleAsync();
        Assert.Equal(7, saved.OrganiserID);
        Assert.Equal("Run", saved.EventType);
        Assert.Equal(saved.EventID, response.EventID);
    }

    [Fact]
    public async Task UpdateEventRejectsAnOrganiserWhoDoesNotOwnIt()
    {
        await using var db = CreateDbContext();
        var raceEvent = CreateEvent(organiserId: 4);
        db.Events.Add(raceEvent);
        await db.SaveChangesAsync();
        var controller = CreateController(db, userId: 7, UserRoles.Organiser);

        var result = await controller.UpdateEvent(raceEvent.EventID, ValidRequest(), CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result.Result).StatusCode);
        Assert.Equal("Spring Race", (await db.Events.SingleAsync()).Name);
    }

    [Fact]
    public async Task DeleteEventReturnsConflictWhileCategoriesExist()
    {
        await using var db = CreateDbContext();
        var raceEvent = CreateEvent(organiserId: 7);
        db.Events.Add(raceEvent);
        await db.SaveChangesAsync();
        db.Categories.Add(new Category
        {
            EventID = raceEvent.EventID,
            CategoryName = "Open"
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db, userId: 7, UserRoles.Organiser);

        var result = await controller.DeleteEvent(raceEvent.EventID, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Single(await db.Events.ToListAsync());
    }

    [Fact]
    public async Task DeleteEventRemovesAnOwnedEventWithNoDependents()
    {
        await using var db = CreateDbContext();
        var raceEvent = CreateEvent(organiserId: 7);
        db.Events.Add(raceEvent);
        await db.SaveChangesAsync();
        var controller = CreateController(db, userId: 7, UserRoles.Organiser);

        var result = await controller.DeleteEvent(raceEvent.EventID, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(await db.Events.ToListAsync());
    }

    [Fact]
    public void EventRequestRejectsUnsupportedTypesAndNonPositiveDistance()
    {
        var invalid = new EventWriteRequest
        {
            Name = "Winter Race",
            EventDate = DateTime.UtcNow,
            Location = "Cape Town",
            Distance = 0,
            EventType = "Swim"
        };
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(invalid, new ValidationContext(invalid), results, true);

        Assert.False(isValid);
        Assert.Contains(results, item => item.MemberNames.Contains(nameof(EventWriteRequest.EventType)));
        Assert.Contains(results, item => item.MemberNames.Contains(nameof(EventWriteRequest.Distance)));
    }

    private static EventWriteRequest ValidRequest() => new()
    {
        Name = " Spring Race ",
        Description = "A road event",
        EventDate = new DateTime(2027, 3, 1),
        Location = " Cape Town ",
        Distance = 21.1m,
        EventType = "run"
    };

    private static RaceEvent CreateEvent(int organiserId) => new()
    {
        OrganiserID = organiserId,
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

    private static EventsController CreateController(RaceDayDbContext db, int userId, string role)
    {
        var session = new TestSession();
        session.SetInt32(SessionKeys.UserId, userId);
        session.SetString(SessionKeys.Role, role);
        var controller = new EventsController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { Session = session } }
        };
        return controller;
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
