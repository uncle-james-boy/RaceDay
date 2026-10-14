using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Authentication;
using RaceDay.Api.Controllers;
using RaceDay.Api.Data;
using RaceDay.Api.Data.Entities;
using Xunit;

namespace RaceDay.Api.Tests;

public sealed class AuthenticationTests
{
    [Fact]
    public async Task RegisterHashesPasswordAndReturnsNoPasswordHash()
    {
        await using var db = CreateDbContext();
        var hasher = new PasswordHasher<User>();
        var controller = CreateController(db, hasher);

        var result = await controller.Register(new RegisterRequest
        {
            FullName = " Alex Runner ",
            Email = "Alex@Example.com",
            Password = "a secret password",
            Role = UserRoles.Participant
        }, CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(result.Result);
        var response = Assert.IsType<UserResponse>(created.Value);
        var savedUser = await db.Users.SingleAsync();
        Assert.Equal("alex@example.com", savedUser.Email);
        Assert.Equal("Alex Runner", savedUser.FullName);
        Assert.NotEqual("a secret password", savedUser.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(savedUser, savedUser.PasswordHash, "a secret password"));
        Assert.Equal(savedUser.UserID, response.UserID);
    }

    [Fact]
    public async Task RegisterRejectsAnEmailThatAlreadyExists()
    {
        await using var db = CreateDbContext();
        db.Users.Add(CreateUser("alex@example.com"));
        await db.SaveChangesAsync();
        var controller = CreateController(db, new PasswordHasher<User>());

        var result = await controller.Register(new RegisterRequest
        {
            FullName = "Alex Runner",
            Email = "ALEX@example.com",
            Password = "a secret password",
            Role = UserRoles.Participant
        }, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Single(await db.Users.ToListAsync());
    }

    [Fact]
    public async Task RegisterRejectsAnUnsupportedRole()
    {
        await using var db = CreateDbContext();
        var controller = CreateController(db, new PasswordHasher<User>());

        var result = await controller.Register(new RegisterRequest
        {
            FullName = "Alex Runner",
            Email = "alex@example.com",
            Password = "a secret password",
            Role = "Administrator"
        }, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        Assert.Empty(await db.Users.ToListAsync());
    }

    [Fact]
    public async Task LoginStoresUserIdAndRoleInServerSession()
    {
        await using var db = CreateDbContext();
        var hasher = new PasswordHasher<User>();
        var user = CreateUser("alex@example.com");
        user.PasswordHash = hasher.HashPassword(user, "a secret password");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var controller = CreateController(db, hasher);
        var session = new TestSession();
        controller.ControllerContext.HttpContext.Session = session;

        var result = await controller.Login(new LoginRequest
        {
            Email = "Alex@Example.com",
            Password = "a secret password"
        }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(user.UserID, session.GetInt32(SessionKeys.UserId));
        Assert.Equal(UserRoles.Participant, session.GetString(SessionKeys.Role));
    }

    [Fact]
    public async Task LoginRejectsAnIncorrectPassword()
    {
        await using var db = CreateDbContext();
        var hasher = new PasswordHasher<User>();
        var user = CreateUser("alex@example.com");
        user.PasswordHash = hasher.HashPassword(user, "correct password");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var controller = CreateController(db, hasher);

        var result = await controller.Login(new LoginRequest
        {
            Email = user.Email,
            Password = "incorrect password"
        }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public void LogoutClearsTheServerSession()
    {
        var session = new TestSession();
        session.SetInt32(SessionKeys.UserId, 12);
        session.SetString(SessionKeys.Role, UserRoles.Participant);
        var controller = CreateController(CreateDbContext(), new PasswordHasher<User>());
        controller.ControllerContext.HttpContext.Session = session;

        var result = controller.Logout();

        Assert.IsType<NoContentResult>(result);
        Assert.Null(session.GetInt32(SessionKeys.UserId));
        Assert.Null(session.GetString(SessionKeys.Role));
    }

    [Fact]
    public void SessionAuthorizationReturns401WithoutSession()
    {
        var context = CreateAuthorizationContext(new TestSession());

        new SessionAuthorizeAttribute(UserRoles.Organiser).OnAuthorization(context);

        Assert.IsType<UnauthorizedResult>(context.Result);
    }

    [Fact]
    public void SessionAuthorizationReturns403ForTheWrongRole()
    {
        var session = new TestSession();
        session.SetInt32(SessionKeys.UserId, 12);
        session.SetString(SessionKeys.Role, UserRoles.Participant);
        var context = CreateAuthorizationContext(session);

        new SessionAuthorizeAttribute(UserRoles.Organiser).OnAuthorization(context);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(context.Result).StatusCode);
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
        PasswordHash = "unused",
        Role = UserRoles.Participant
    };

    private static AuthController CreateController(RaceDayDbContext db, IPasswordHasher<User> hasher)
    {
        var controller = new AuthController(db, hasher);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { Session = new TestSession() }
        };
        return controller;
    }

    private static AuthorizationFilterContext CreateAuthorizationContext(ISession session)
    {
        var httpContext = new DefaultHttpContext { Session = session };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, []);
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
