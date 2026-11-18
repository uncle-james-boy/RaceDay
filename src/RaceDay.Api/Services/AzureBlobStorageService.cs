using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace RaceDay.Api.Services;

/// <summary>Azure SDK implementation used only by the API.</summary>
public sealed class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly BlobStorageOptions _options;

    public AzureBlobStorageService(
        BlobServiceClient blobServiceClient,
        IOptions<BlobStorageOptions> options)
    {
        _blobServiceClient = blobServiceClient;
        _options = options.Value;
        _options.Validate();
    }

    public async Task<Uri> UploadAsync(
        RaceDayBlobContainer container,
        string blobName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ValidateBlobName(blobName);

        var blob = (container == RaceDayBlobContainer.ProfilePictures
            ? await GetPrivateProfileContainerAsync(cancellationToken)
            : GetContainer(container)).GetBlobClient(blobName);
        await blob.UploadAsync(
            content,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
                Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
            },
            cancellationToken);

        if (container == RaceDayBlobContainer.EventBanners
            && !string.IsNullOrWhiteSpace(_options.EventBannersPublicBaseUrl))
        {
            var publicBase = _options.EventBannersPublicBaseUrl.TrimEnd('/');
            return new Uri($"{publicBase}/{_options.EventBannersContainerName}/{Uri.EscapeDataString(blobName)}");
        }

        return blob.Uri;
    }

    public async Task<StoredBlob?> DownloadAsync(
        RaceDayBlobContainer container,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        ValidateBlobName(blobName);
        var targetContainer = container == RaceDayBlobContainer.ProfilePictures
            ? await GetPrivateProfileContainerAsync(cancellationToken)
            : GetContainer(container);
        var blob = targetContainer.GetBlobClient(blobName);

        try
        {
            var download = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
            return new StoredBlob(download.Value.Content, download.Value.Details.ContentType);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task<bool> DeleteIfExistsAsync(
        RaceDayBlobContainer container,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        ValidateBlobName(blobName);
        var targetContainer = container == RaceDayBlobContainer.ProfilePictures
            ? await GetPrivateProfileContainerAsync(cancellationToken)
            : GetContainer(container);
        var response = await targetContainer
            .GetBlobClient(blobName)
            .DeleteIfExistsAsync(cancellationToken: cancellationToken);
        return response.Value;
    }

    public bool TryGetBlobNameFromUri(RaceDayBlobContainer container, Uri blobUri, out string blobName)
    {
        blobName = string.Empty;
        if (!blobUri.IsAbsoluteUri || !string.IsNullOrEmpty(blobUri.Query)
            || !string.IsNullOrEmpty(blobUri.Fragment) || !string.IsNullOrEmpty(blobUri.UserInfo))
        {
            return false;
        }

        var containerClient = GetContainer(container);
        if (TryMatchBlobUri(containerClient.Uri, blobUri, out blobName))
        {
            return true;
        }

        if (container == RaceDayBlobContainer.EventBanners
            && !string.IsNullOrWhiteSpace(_options.EventBannersPublicBaseUrl))
        {
            var publicContainerUri = new Uri(
                $"{_options.EventBannersPublicBaseUrl.TrimEnd('/')}/{_options.EventBannersContainerName}");
            return TryMatchBlobUri(publicContainerUri, blobUri, out blobName);
        }

        return false;
    }

    private static bool TryMatchBlobUri(Uri containerUri, Uri blobUri, out string blobName)
    {
        blobName = string.Empty;
        if (!blobUri.IsAbsoluteUri || !string.IsNullOrEmpty(blobUri.Query)
            || !string.IsNullOrEmpty(blobUri.Fragment) || !string.IsNullOrEmpty(blobUri.UserInfo))
        {
            return false;
        }

        var prefix = containerUri.AbsolutePath.TrimEnd('/') + "/";
        if (!blobUri.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var escapedName = blobUri.AbsolutePath[prefix.Length..];
        if (escapedName.Length == 0 || escapedName.Contains('/'))
        {
            return false;
        }

        string decodedName;
        try
        {
            decodedName = Uri.UnescapeDataString(escapedName);
        }
        catch (UriFormatException)
        {
            return false;
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(
                decodedName,
                "^[a-f0-9]{32}\\.(jpg|png|webp)$",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            return false;
        }

        var expectedPath = $"{prefix}{Uri.EscapeDataString(decodedName)}";
        if (!string.Equals(blobUri.Scheme, containerUri.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(blobUri.Host, containerUri.Host, StringComparison.OrdinalIgnoreCase)
            || blobUri.Port != containerUri.Port
            || !string.Equals(blobUri.AbsolutePath, expectedPath, StringComparison.Ordinal))
        {
            return false;
        }

        blobName = decodedName;
        return true;
    }

    private BlobContainerClient GetContainer(RaceDayBlobContainer container) => container switch
    {
        RaceDayBlobContainer.EventBanners => _blobServiceClient.GetBlobContainerClient(_options.EventBannersContainerName),
        RaceDayBlobContainer.ProfilePictures => _blobServiceClient.GetBlobContainerClient(_options.ProfilePicturesContainerName),
        _ => throw new ArgumentOutOfRangeException(nameof(container), container, "Unknown RaceDay Blob container.")
    };

    private async Task<BlobContainerClient> GetPrivateProfileContainerAsync(CancellationToken cancellationToken)
    {
        var container = GetContainer(RaceDayBlobContainer.ProfilePictures);
        var accessPolicy = await container.GetAccessPolicyAsync(cancellationToken: cancellationToken);
        if (accessPolicy.Value.BlobPublicAccess != PublicAccessType.None)
        {
            throw new InvalidOperationException("The profile-pictures Blob container must have private access.");
        }

        return container;
    }

    private static void ValidateBlobName(string? blobName)
    {
        if (string.IsNullOrWhiteSpace(blobName)
            || blobName.Length > 200
            || blobName.Contains('/')
            || blobName.Contains('\\')
            || blobName is "." or "..")
        {
            throw new ArgumentException("Blob names must be a single safe path segment.", nameof(blobName));
        }
    }
}
