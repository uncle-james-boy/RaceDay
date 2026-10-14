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

public sealed class ResultTests
{
    [Fact]
    public async Task EventOrganiserCanRecordAResultAfterTheEvent()
    {
        await using var db = CreateDbContext();
        var entry = await AddEnrolment(db, organiserId: 3, participantId: 8, DateTime.Now.AddDays(-2));
        var controller = CreateController(db, userId: 3, UserRoles.Organiser);

        var result = await controller.Create(new CreateRaceResultRequest
        {
            EnrolmentID = entry.Enrolment.EnrolmentID,
            FinishTime = TimeSpan.FromHours(2),
            Position = 4
        }, CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(result.Result);
        var response = Assert.IsType<RaceResultResponse>(created.Value);
        Assert.Equal("Spring Race", response.EventName);
        Assert.Equal("Open", response.CategoryName);
        Assert.Equal(TimeSpan.FromHours(2), response.FinishTime);
        Assert.Equal(4, response.Position);
        Assert.Single(await db.Results.ToListAsync());
    }

    [Fact]
    public async Task OrganiserCannotRecordResultsBeforeTheEventDate()
    {
        await using var db = CreateDbContext();
        var entry = await AddEnrolment(db, organiserId: 3, participantId: 8, DateTime.Now.AddDays(1));
        var controller = CreateController(db, userId: 3, UserRoles.Organiser);

        var result = await controller.Create(new CreateRaceResultRequest
        {
            EnrolmentID = entry.Enrolment.EnrolmentID,
            FinishTime = TimeSpan.FromHours(2),
            Position = 4
        }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(await db.Results.ToListAsync());
    }

    [Fact]
    public async Task DifferentOrganiserCannotRecordResultForAnotherEvent()
    {
        await using var db = CreateDbContext();
        var entry = await AddEnrolment(db, organiserId: 3, participantId: 8, DateTime.Now.AddDays(-2));
        var controller = CreateController(db, userId: 4, UserRoles.Organiser);

        var result = await controller.Create(new CreateRaceResultRequest
        {
            EnrolmentID = entry.Enrolment.EnrolmentID,
            FinishTime = TimeSpan.FromHours(2),
            Position = 4
        }, CancellationToken.None);

        Assert.IsType<ForbidResult>(result.Result);
        Assert.Empty(await db.Results.ToListAsync());
    }

    [Fact]
    public async Task ParticipantCanViewOnlyTheirOwnResults()
    {
        await using var db = CreateDbContext();
        var current = await AddEnrolment(db, organiserId: 3, participantId: 8, DateTime.Now.AddDays(-2));
        var other = await AddEnrolment(db, organiserId: 3, participantId: 9, DateTime.Now.AddDays(-2), "Autumn Race");
        db.Results.AddRange(
            CreateResult(current.Enrolment, TimeSpan.FromHours(2), 4),
            CreateResult(other.Enrolment, TimeSpan.FromHours(3), 8));
        await db.SaveChangesAsync();
        var controller = CreateController(db, userId: 8, UserRoles.Participant);

        var result = await controller.GetMine(CancellationToken.None);

        var rows = Assert.IsType<List<RaceResultResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Single(rows);
        Assert.Equal(current.Enrolment.EnrolmentID, rows[0].EnrolmentID);
        Assert.Equal("Spring Race", rows[0].EventName);
    }

    [Fact]
    public void ResultRequestRejectsNonPositiveTimeAndPosition()
    {
        var invalid = new CreateRaceResultRequest { EnrolmentID = 1, FinishTime = TimeSpan.Zero, Position = 1 };
        var errors = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(invalid, new ValidationContext(invalid), errors, true);

        Assert.False(isValid);
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(CreateRaceResultRequest.FinishTime)));
    }

    private static async Task<(Enrolment Enrolment, User Participant)> AddEnrolment(
        RaceDayDbContext db,
        int organiserId,
        int participantId,
        DateTime eventDate,
        string eventName = "Spring Race")
    {
        var organiser = await db.Users.SingleOrDefaultAsync(user => user.UserID == organiserId);
        if (organiser is null)
        {
            organiser = CreateUser(organiserId, "organiser@example.com", UserRoles.Organiser);
            db.Users.Add(organiser);
        }
        var participant = CreateUser(participantId, $"runner{participantId}@example.com", UserRoles.Participant);
        db.Users.Add(participant);
        var raceEvent = new RaceEvent
        {
            OrganiserID = organiserId,
            Organiser = organiser,
            Name = eventName,
            EventDate = eventDate,
            Location = "Cape Town",
            Distance = 21.1m,
            EventType = "Run"
        };
        db.Events.Add(raceEvent);
        await db.SaveChangesAsync();
        var category = new Category
        {
            EventID = raceEvent.EventID,
            Event = raceEvent,
            CategoryName = "Open"
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        var enrolment = new Enrolment
        {
            EventID = raceEvent.EventID,
            Event = raceEvent,
            CategoryID = category.CategoryID,
            Category = category,
            ParticipantID = participant.UserID,
            Participant = participant,
            EnrolmentDate = DateTime.Now.AddDays(-5),
            Status = "Confirmed"
        };
        db.Enrolments.Add(enrolment);
        await db.SaveChangesAsync();
        return (enrolment, participant);
    }

    private static RaceResult CreateResult(Enrolment enrolment, TimeSpan time, int position) => new()
    {
        EnrolmentID = enrolment.EnrolmentID,
        Enrolment = enrolment,
        FinishTime = time,
        Position = position,
        CapturedAt = DateTime.UtcNow
    };

    private static User CreateUser(int id, string email, string role) => new()
    {
        UserID = id,
        FullName = "Alex Runner",
        Email = email,
        PasswordHash = "hashed-password",
        Role = role
    };

    private static RaceDayDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<RaceDayDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new RaceDayDbContext(options);
    }

    private static ResultsController CreateController(RaceDayDbContext db, int userId, string role)
    {
        var session = new TestSession();
        session.SetInt32(SessionKeys.UserId, userId);
        session.SetString(SessionKeys.Role, role);
        return new ResultsController(db)
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
