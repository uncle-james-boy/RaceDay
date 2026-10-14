using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Authentication;
using RaceDay.Api.Data;
using RaceDay.Api.Data.Entities;

namespace RaceDay.Api.Controllers;

/// <summary>Reads and updates the profile belonging to the signed-in user.</summary>
[ApiController]
[Route("api/users")]
[SessionAuthorize]
public sealed class UsersController(RaceDayDbContext dbContext) : ControllerBase
{
    /// <summary>Returns the profile for the account in the current server session.</summary>
    [HttpGet("profile")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileResponse>> GetProfile(CancellationToken cancellationToken)
    {
        var userId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
        var user = await dbContext.Users.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserID == userId, cancellationToken);

        return user is null ? NotFound() : Ok(ToResponse(user));
    }

    /// <summary>Updates only the name and profile picture URL of the signed-in account.</summary>
    [HttpPut("profile")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileResponse>> UpdateProfile(
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
        var user = await dbContext.Users
            .SingleOrDefaultAsync(item => item.UserID == userId, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        user.FullName = request.FullName.Trim();
        user.ProfilePictureUrl = string.IsNullOrWhiteSpace(request.ProfilePictureUrl)
            ? null
            : request.ProfilePictureUrl.Trim();

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(user));
    }

    private static UserProfileResponse ToResponse(User user) =>
        new(user.UserID, user.FullName, user.Email, user.Role, user.ProfilePictureUrl, user.CreatedAt);
}

public sealed class UpdateProfileRequest
{
    [Required, StringLength(100)]
    public string FullName { get; init; } = string.Empty;

    [StringLength(500), Url]
    public string? ProfilePictureUrl { get; init; }
}

public sealed record UserProfileResponse(
    int UserID,
    string FullName,
    string Email,
    string Role,
    string? ProfilePictureUrl,
    DateTime CreatedAt);
