using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Authentication;
using RaceDay.Api.Controllers;
using RaceDay.Api.Data;
using RaceDay.Api.Data.Entities;
using Xunit;

namespace RaceDay.Api.Tests;

public sealed class EnrolmentTests
{
    [Fact]
    public async Task ParticipantCanEnrolAndTheSessionUserIsRecorded()
    {
        await using var db = CreateDbContext();
        var data = await SeedEventAndCategory(db, organiserId: 3);
        await AddParticipant(db, userId: 8, "runner@example.com");
        var controller = CreateParticipantController(db, userId: 8);

        var result = await controller.Create(new CreateEnrolmentRequest
        {
            EventID = data.Event.EventID,
            CategoryID = data.Category.CategoryID
        }, CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(result.Result);
        var response = Assert.IsType<EnrolmentResponse>(created.Value);
        var saved = await db.Enrolments.SingleAsync();
        Assert.Equal(8, saved.ParticipantID);
        Assert.Equal(data.Event.EventID, saved.EventID);
        Assert.Equal(data.Category.CategoryID, saved.CategoryID);
        Assert.Equal("Pending", saved.Status);
        Assert.Equal("Spring Race", response.EventName);
        Assert.Equal("Alex Runner", response.ParticipantName);
    }

    [Fact]
    public async Task ParticipantCannotEnrolInCategoryFromAnotherEvent()
    {
        await using var db = CreateDbContext();
        var first = await SeedEventAndCategory(db, organiserId: 3);
        var second = await SeedEventAndCategory(db, organiserId: 3, name: "Autumn Race");
        var controller = CreateParticipantController(db, userId: 8);

        var result = await controller.Create(new CreateEnrolmentRequest
        {
            EventID = first.Event.EventID,
            CategoryID = second.Category.CategoryID
        }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(await db.Enrolments.ToListAsync());
    }

    [Fact]
    public async Task ParticipantCannotCreateDuplicateEnrolment()
    {
        await using var db = CreateDbContext();
        var data = await SeedEventAndCategory(db, organiserId: 3);
        var participant = await AddParticipant(db, userId: 8, "runner@example.com");
        db.Enrolments.Add(CreateEnrolment(data.Event, data.Category, participant));
        await db.SaveChangesAsync();
        var controller = CreateParticipantController(db, userId: 8);

        var result = await controller.Create(new CreateEnrolmentRequest
        {
            EventID = data.Event.EventID,
            CategoryID = data.Category.CategoryID
        }, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Single(await db.Enrolments.ToListAsync());
    }

    [Fact]
    public async Task ParticipantListContainsOnlyTheCurrentUsersEnrolments()
    {
        await using var db = CreateDbContext();
        var data = await SeedEventAndCategory(db, organiserId: 3);
        var current = await AddParticipant(db, userId: 8, "runner@example.com");
        var other = await AddParticipant(db, userId: 9, "other@example.com");
        db.Enrolments.AddRange(
            CreateEnrolment(data.Event, data.Category, current),
            CreateEnrolment(data.Event, data.Category, other));
        await db.SaveChangesAsync();

        var result = await CreateParticipantController(db, userId: 8).GetMine(CancellationToken.None);

        var rows = Assert.IsType<List<EnrolmentResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Single(rows);
        Assert.Equal(8, rows[0].ParticipantID);
    }

    [Fact]
    public async Task OrganiserCanListOnlyEnrolmentsForTheirOwnEvent()
    {
        await using var db = CreateDbContext();
        var data = await SeedEventAndCategory(db, organiserId: 3);
        var participant = await AddParticipant(db, userId: 8, "runner@example.com");
        db.Enrolments.Add(CreateEnrolment(data.Event, data.Category, participant));
        await db.SaveChangesAsync();

        var forbidden = await CreateOrganiserController(db, userId: 4)
            .GetForEvent(data.Event.EventID, CancellationToken.None);
        var allowed = await CreateOrganiserController(db, userId: 3)
            .GetForEvent(data.Event.EventID, CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(forbidden.Result).StatusCode);
        var rows = Assert.IsType<List<EnrolmentResponse>>(Assert.IsType<OkObjectResult>(allowed.Result).Value);
        Assert.Single(rows);
        Assert.Equal("Alex Runner", rows[0].ParticipantName);
    }

    private static async Task<(RaceEvent Event, Category Category)> SeedEventAndCategory(
        RaceDayDbContext db,
        int organiserId,
        string name = "Spring Race")
    {
        if (!await db.Users.AnyAsync(user => user.UserID == organiserId))
        {
            var organiser = CreateUser(organiserId, "organiser@example.com", UserRoles.Organiser);
            db.Users.Add(organiser);
        }
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
        var category = new Category { EventID = raceEvent.EventID, CategoryName = "Open" };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return (raceEvent, category);
    }

    private static async Task<User> AddParticipant(RaceDayDbContext db, int userId, string email)
    {
        var participant = CreateUser(userId, email, UserRoles.Participant);
        db.Users.Add(participant);
        await db.SaveChangesAsync();
        return participant;
    }

    private static User CreateUser(int id, string email, string role) => new()
    {
        UserID = id,
        FullName = "Alex Runner",
        Email = email,
        PasswordHash = "hashed-password",
        Role = role
    };

    private static Enrolment CreateEnrolment(RaceEvent raceEvent, Category category, User participant) => new()
    {
        EventID = raceEvent.EventID,
        CategoryID = category.CategoryID,
        ParticipantID = participant.UserID,
        Event = raceEvent,
        Category = category,
        Participant = participant,
        EnrolmentDate = new DateTime(2026, 10, 1),
        Status = "Pending"
    };

    private static RaceDayDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<RaceDayDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new RaceDayDbContext(options);
    }

    private static EnrolmentsController CreateParticipantController(RaceDayDbContext db, int userId) =>
        CreateController(db, userId, UserRoles.Participant);

    private static EnrolmentsController CreateOrganiserController(RaceDayDbContext db, int userId) =>
        CreateController(db, userId, UserRoles.Organiser);

    private static EnrolmentsController CreateController(RaceDayDbContext db, int userId, string role)
    {
        var session = new TestSession();
        session.SetInt32(SessionKeys.UserId, userId);
        session.SetString(SessionKeys.Role, role);
        return new EnrolmentsController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { Session = session } }
        };
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
