using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Authentication;
using RaceDay.Api.Controllers;
using RaceDay.Api.Data;
using RaceDay.Api.Data.Entities;
using Xunit;

namespace RaceDay.Api.Tests;

public sealed class ProfileTests
{
    [Fact]
    public async Task GetProfileReturnsOnlyTheUserStoredInTheSession()
    {
        await using var db = CreateDbContext();
        var current = CreateUser("current@example.com");
        var other = CreateUser("other@example.com");
        db.Users.AddRange(current, other);
        await db.SaveChangesAsync();
        var controller = CreateController(db, current.UserID);

        var result = await controller.GetProfile(CancellationToken.None);

        var response = Assert.IsType<UserProfileResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(current.UserID, response.UserID);
        Assert.Equal(current.Email, response.Email);
        Assert.DoesNotContain("PasswordHash", typeof(UserProfileResponse).GetProperties().Select(p => p.Name));
    }

    [Fact]
    public async Task UpdateProfileChangesOnlyTheCurrentUserAndKeepsTheirRoleAndEmail()
    {
        await using var db = CreateDbContext();
        var current = CreateUser("current@example.com");
        var other = CreateUser("other@example.com");
        db.Users.AddRange(current, other);
        await db.SaveChangesAsync();
        var controller = CreateController(db, current.UserID);

        var result = await controller.UpdateProfile(new UpdateProfileRequest
        {
            FullName = "  New Name  ",
            ProfilePictureUrl = "https://example.com/profile.png"
        }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        var savedCurrent = await db.Users.SingleAsync(user => user.UserID == current.UserID);
        var savedOther = await db.Users.SingleAsync(user => user.UserID == other.UserID);
        Assert.Equal("New Name", savedCurrent.FullName);
        Assert.Equal("current@example.com", savedCurrent.Email);
        Assert.Equal(UserRoles.Participant, savedCurrent.Role);
        Assert.Equal("hashed-password", savedCurrent.PasswordHash);
        Assert.Equal("https://example.com/profile.png", savedCurrent.ProfilePictureUrl);
        Assert.Equal("Alex Runner", savedOther.FullName);
    }

    private static RaceDayDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<RaceDayDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new RaceDayDbContext(options);
    }

    private static User CreateUser(string email) => new()
    {
        FullName = "Alex Runner",
        Email = email,
        PasswordHash = "hashed-password",
        Role = UserRoles.Participant
    };

    private static UsersController CreateController(RaceDayDbContext db, int userId)
    {
        var session = new TestSession();
        session.SetInt32(SessionKeys.UserId, userId);
        session.SetString(SessionKeys.Role, UserRoles.Participant);
        var controller = new UsersController(db);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { Session = session }
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
