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
    public async Task GetEventsAppliesSearchTypeAndInclusiveDateFilters()
    {
        await using var db = CreateDbContext();
        db.Events.AddRange(
            CreateEvent(7),
            new RaceEvent
            {
                OrganiserID = 8,
                Name = "Summer Cycle",
                EventDate = new DateTime(2027, 6, 15, 9, 30, 0),
                Location = "Durban",
                Distance = 40,
                EventType = "Cycle"
            },
            new RaceEvent
            {
                OrganiserID = 8,
                Name = "Spring Run",
                EventDate = new DateTime(2027, 3, 2),
                Location = "Cape Town",
                Distance = 10,
                EventType = "Run"
            });
        await db.SaveChangesAsync();
        var controller = CreateController(db, userId: 2, UserRoles.Participant);

        var result = await controller.GetEvents(new EventSearchRequest
        {
            Search = "cape",
            EventType = "run",
            FromDate = new DateOnly(2027, 3, 1),
            ToDate = new DateOnly(2027, 3, 1)
        }, CancellationToken.None);

        var events = Assert.IsType<List<EventResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Single(events);
        Assert.Equal("Spring Race", events[0].Name);
    }

    [Fact]
    public async Task OrganiserEventSummaryReturnsOnlyOwnedEventsAndEnrolmentCounts()
    {
        await using var db = CreateDbContext();
        var own = CreateEvent(7);
        var other = CreateEvent(8);
        db.Events.AddRange(own, other);
        await db.SaveChangesAsync();
        var category = new Category { EventID = own.EventID, CategoryName = "Open" };
        var participant = new User
        {
            FullName = "Runner", Email = "runner@example.test", PasswordHash = "hash", Role = UserRoles.Participant
        };
        db.Users.Add(participant);
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        db.Enrolments.Add(new Enrolment
        {
            EventID = own.EventID, CategoryID = category.CategoryID, ParticipantID = participant.UserID,
            Status = "Pending", EnrolmentDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db, userId: 7, UserRoles.Organiser);

        var result = await controller.GetMine(CancellationToken.None);

        var rows = Assert.IsType<List<OrganiserEventSummaryResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        var row = Assert.Single(rows);
        Assert.Equal(own.EventID, row.EventID);
        Assert.Equal(1, row.EnrolmentCount);
    }

    [Fact]
    public void EventSearchRejectsUnsupportedTypesAndReversedDates()
    {
        var invalid = new EventSearchRequest
        {
            EventType = "Swim",
            FromDate = new DateOnly(2027, 4, 2),
            ToDate = new DateOnly(2027, 4, 1)
        };
        var errors = invalid.Validate(new ValidationContext(invalid)).ToList();

        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(EventSearchRequest.EventType)));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(EventSearchRequest.FromDate)));
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
