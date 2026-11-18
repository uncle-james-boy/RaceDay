namespace RaceDay.Api.Services;

/// <summary>Names the only Blob containers the RaceDay API is allowed to use.</summary>
public enum RaceDayBlobContainer
{
    EventBanners,
    ProfilePictures
}

/// <summary>A downloaded blob stream and the content type stored with it.</summary>
public sealed record StoredBlob(Stream Content, string? ContentType);

/// <summary>API-owned operations for image blobs. This service never creates containers or changes access policies.</summary>
public interface IBlobStorageService
{
    Task<Uri> UploadAsync(
        RaceDayBlobContainer container,
        string blobName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<StoredBlob?> DownloadAsync(
        RaceDayBlobContainer container,
        string blobName,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteIfExistsAsync(
        RaceDayBlobContainer container,
        string blobName,
        CancellationToken cancellationToken = default);

    bool TryGetBlobNameFromUri(RaceDayBlobContainer container, Uri blobUri, out string blobName);
}
