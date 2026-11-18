using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Authentication;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>Uploads promotional banners for events owned by the signed-in Organiser.</summary>
[ApiController]
[Route("api/events")]
public sealed class EventBannersController(
    RaceDayDbContext dbContext,
    IBlobStorageService blobStorage,
    ILogger<EventBannersController> logger) : ControllerBase
{
    public const long MaximumFileSizeBytes = 5L * 1024 * 1024;
    private const long MultipartOverheadBytes = 64L * 1024;
    private const int MaximumStoredUrlLength = 500;

    /// <summary>Uploads or replaces the banner for an event owned by the current Organiser.</summary>
    [HttpPost("{eventId:int}/banner")]
    [SessionAuthorize(UserRoles.Organiser)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumFileSizeBytes + MultipartOverheadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaximumFileSizeBytes)]
    [ProducesResponseType(typeof(EventBannerUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<EventBannerUploadResponse>> Upload(
        int eventId,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Choose a non-empty JPEG, PNG, or WebP image." });
        }

        if (file.Length > MaximumFileSizeBytes)
        {
            return BadRequest(new { message = "Event banner images must be 5 MB or smaller." });
        }

        var organiserId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
        var raceEvent = await dbContext.Events
            .SingleOrDefaultAsync(item => item.EventID == eventId, cancellationToken);
        if (raceEvent is null) return NotFound();
        if (raceEvent.OrganiserID != organiserId) return StatusCode(StatusCodes.Status403Forbidden);

        byte[] imageBytes;
        await using (var buffer = new MemoryStream((int)file.Length))
        {
            await file.CopyToAsync(buffer, cancellationToken);
            if (buffer.Length == 0 || buffer.Length > MaximumFileSizeBytes)
            {
                return BadRequest(new { message = "Event banner images must be non-empty and 5 MB or smaller." });
            }
            imageBytes = buffer.ToArray();
        }

        if (!ImageContentValidator.TryValidate(file.FileName, file.ContentType, imageBytes,
                out var extension, out var contentType))
        {
            return BadRequest(new { message = "Upload a valid JPEG, PNG, or WebP image with a matching file type." });
        }

        var blobName = $"{Guid.NewGuid():N}{extension}";
        Uri bannerUri;
        try
        {
            await using var imageStream = new MemoryStream(imageBytes, writable: false);
            bannerUri = await blobStorage.UploadAsync(
                RaceDayBlobContainer.EventBanners, blobName, imageStream, contentType, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Event banner upload failed for event {EventId}.", eventId);
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "The banner could not be uploaded. Check the storage service and try again." });
        }

        if (bannerUri.AbsoluteUri.Length > MaximumStoredUrlLength)
        {
            await TryDeleteNewBlobAsync(blobName, eventId);
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "The storage URL is too long for the event banner field." });
        }

        var oldUrl = raceEvent.EventBannerUrl;
        raceEvent.EventBannerUrl = bannerUri.AbsoluteUri;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            raceEvent.EventBannerUrl = oldUrl;
            logger.LogError(exception, "Saving the banner URL failed for event {EventId}.", eventId);
            await TryDeleteNewBlobAsync(blobName, eventId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "The banner was uploaded but could not be saved. The previous banner was kept." });
        }

        await TryDeleteOldBannerAsync(oldUrl, eventId);
        return Ok(new EventBannerUploadResponse(eventId, bannerUri.AbsoluteUri));
    }

    private async Task TryDeleteOldBannerAsync(string? oldUrl, int eventId)
    {
        if (!Uri.TryCreate(oldUrl, UriKind.Absolute, out var oldUri)
            || !blobStorage.TryGetBlobNameFromUri(RaceDayBlobContainer.EventBanners, oldUri, out var oldBlobName))
        {
            return;
        }

        try
        {
            await blobStorage.DeleteIfExistsAsync(
                RaceDayBlobContainer.EventBanners, oldBlobName, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception,
                "The previous banner could not be cleaned up for event {EventId}; the new banner remains active.", eventId);
        }
    }

    private async Task TryDeleteNewBlobAsync(string blobName, int eventId)
    {
        try
        {
            await blobStorage.DeleteIfExistsAsync(
                RaceDayBlobContainer.EventBanners, blobName, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Cleanup of a newly uploaded banner failed for event {EventId}.", eventId);
        }
    }
}

public sealed record EventBannerUploadResponse(int EventID, string EventBannerUrl);
