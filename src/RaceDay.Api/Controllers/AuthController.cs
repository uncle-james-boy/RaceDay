using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Authentication;
using RaceDay.Api.Data;
using RaceDay.Api.Data.Entities;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    RaceDayDbContext dbContext,
    IPasswordHasher<User> passwordHasher) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (request.Role is not UserRoles.Organiser and not UserRoles.Participant)
        {
            return BadRequest(new { message = "Role must be Organiser or Participant." });
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await dbContext.Users.AnyAsync(user => user.Email == email, cancellationToken))
        {
            return Conflict(new { message = "An account with this email already exists." });
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = string.Empty,
            Role = request.Role
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Created("/api/users/profile", ToResponse(user));
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.SingleOrDefaultAsync(
            candidate => candidate.Email == email,
            cancellationToken);

        if (user is null || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password)
            == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = "Email or password is incorrect." });
        }

        HttpContext.Session.Clear();
        HttpContext.Session.SetInt32(SessionKeys.UserId, user.UserID);
        HttpContext.Session.SetString(SessionKeys.Role, user.Role);

        return Ok(ToResponse(user));
    }

    [HttpPost("logout")]
    [SessionAuthorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return NoContent();
    }

    private static UserResponse ToResponse(User user) =>
        new(user.UserID, user.FullName, user.Email, user.Role, user.ProfilePictureUrl);
}

public sealed class RegisterRequest
{
    [Required, StringLength(100)]
    public string FullName { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string Password { get; init; } = string.Empty;

    [Required, StringLength(20)]
    public string Role { get; init; } = string.Empty;
}

public sealed class LoginRequest
{
    [Required, EmailAddress, StringLength(150)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string Password { get; init; } = string.Empty;
}

public sealed record UserResponse(int UserID, string FullName, string Email, string Role, string? ProfilePictureUrl);
