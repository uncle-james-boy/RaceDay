using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Authentication;
using RaceDay.Api.Data;
using RaceDay.Api.Data.Entities;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>Reads and updates the profile belonging to the signed-in user.</summary>
[ApiController]
[Route("api/users")]
[SessionAuthorize]
public sealed class UsersController(
    RaceDayDbContext dbContext,
    IBlobStorageService blobStorage,
    ILogger<UsersController> logger) : ControllerBase
{
    public const long MaximumProfilePictureSizeBytes = 2L * 1024 * 1024;
    private const long MultipartOverheadBytes = 64L * 1024;
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

    /// <summary>Uploads or replaces the profile picture of the signed-in user.</summary>
    [HttpPost("profile/picture")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumProfilePictureSizeBytes + MultipartOverheadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaximumProfilePictureSizeBytes)]
    [ProducesResponseType(typeof(ProfilePictureUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ProfilePictureUploadResponse>> UploadProfilePicture(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Choose a non-empty JPEG, PNG, or WebP profile picture." });
        }

        if (file.Length > MaximumProfilePictureSizeBytes)
        {
            return BadRequest(new { message = "Profile pictures must be 2 MB or smaller." });
        }

        var userId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.UserID == userId, cancellationToken);
        if (user is null) return NotFound();

        byte[] imageBytes;
        await using (var buffer = new MemoryStream((int)file.Length))
        {
            await file.CopyToAsync(buffer, cancellationToken);
            if (buffer.Length == 0 || buffer.Length > MaximumProfilePictureSizeBytes)
            {
                return BadRequest(new { message = "Profile pictures must be non-empty and 2 MB or smaller." });
            }

            imageBytes = buffer.ToArray();
        }

        if (!ImageContentValidator.TryValidate(file.FileName, file.ContentType, imageBytes,
                out var extension, out var contentType))
        {
            return BadRequest(new { message = "Upload a valid JPEG, PNG, or WebP image with a matching file type." });
        }

        var blobName = $"{Guid.NewGuid():N}{extension}";
        try
        {
            await using var imageStream = new MemoryStream(imageBytes, writable: false);
            await blobStorage.UploadAsync(
                RaceDayBlobContainer.ProfilePictures, blobName, imageStream, contentType, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Profile picture upload failed for user {UserId}.", userId);
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "The profile picture could not be uploaded. Check storage and try again." });
        }

        var oldReference = user.ProfilePictureUrl;
        user.ProfilePictureUrl = blobName;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            user.ProfilePictureUrl = oldReference;
            logger.LogError(exception, "Saving the profile picture reference failed for user {UserId}.", userId);
            await TryDeleteProfileBlobAsync(blobName, userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "The picture was uploaded but could not be saved. Your previous picture was kept." });
        }

        if (TryGetProfileBlobName(oldReference, out var oldBlobName))
        {
            await TryDeleteProfileBlobAsync(oldBlobName, userId);
        }

        Response.Headers.CacheControl = "no-store, private";
        return Ok(new ProfilePictureUploadResponse(HasProfilePicture: true));
    }

    /// <summary>Streams the signed-in user's private profile picture.</summary>
    [HttpGet("profile/picture")]
    [Produces("image/jpeg", "image/png", "image/webp")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetProfilePicture(CancellationToken cancellationToken)
    {
        var userId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
        var reference = await dbContext.Users.AsNoTracking()
            .Where(item => item.UserID == userId)
            .Select(item => item.ProfilePictureUrl)
            .SingleOrDefaultAsync(cancellationToken);
        if (!TryGetProfileBlobName(reference, out var blobName)) return NotFound();

        StoredBlob? storedBlob;
        try
        {
            storedBlob = await blobStorage.DownloadAsync(
                RaceDayBlobContainer.ProfilePictures, blobName, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Profile picture retrieval failed for user {UserId}.", userId);
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "The profile picture is temporarily unavailable." });
        }

        if (storedBlob is null)
        {
            return NotFound();
        }

        var contentType = storedBlob.ContentType;
        if (contentType is not "image/jpeg" and not "image/png" and not "image/webp")
        {
            await storedBlob.Content.DisposeAsync();
            logger.LogError("Profile picture blob for user {UserId} had an unsupported content type.", userId);
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "The stored profile picture has an invalid format." });
        }

        Response.Headers.CacheControl = "no-store, private";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(storedBlob.Content, contentType);
    }

    /// <summary>Updates the name of the signed-in account.</summary>
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

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(user));
    }

    private static UserProfileResponse ToResponse(User user) =>
        new(user.UserID, user.FullName, user.Email, user.Role,
            TryGetProfileBlobName(user.ProfilePictureUrl, out _), user.CreatedAt);

    internal static bool TryGetProfileBlobName(string? reference, out string blobName)
    {
        blobName = string.Empty;
        if (reference is null || reference.Length is not (36 or 37)
            || !System.Text.RegularExpressions.Regex.IsMatch(
                reference, "\\A[a-f0-9]{32}\\.(jpg|png|webp)\\z",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            return false;
        }

        blobName = reference;
        return true;
    }

    private async Task TryDeleteProfileBlobAsync(string blobName, int userId)
    {
        try
        {
            await blobStorage.DeleteIfExistsAsync(
                RaceDayBlobContainer.ProfilePictures, blobName, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Profile picture cleanup failed for user {UserId}.", userId);
        }
    }
}

public sealed class UpdateProfileRequest
{
    [Required, StringLength(100)]
    public string FullName { get; init; } = string.Empty;

}

public sealed record UserProfileResponse(
    int UserID,
    string FullName,
    string Email,
    string Role,
    bool HasProfilePicture,
    DateTime CreatedAt);

public sealed record ProfilePictureUploadResponse(bool HasProfilePicture);
