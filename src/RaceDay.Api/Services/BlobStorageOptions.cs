using System.Text.RegularExpressions;

namespace RaceDay.Api.Services;

/// <summary>Configuration for the API's two image containers.</summary>
public sealed class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";

    public string ConnectionString { get; set; } = string.Empty;

    public string EventBannersContainerName { get; set; } = "event-banners";

    public string ProfilePicturesContainerName { get; set; } = "profile-pictures";

    /// <summary>
    /// Optional browser-visible endpoint prefix for event banners. This is useful when
    /// the API reaches Azurite by its Docker service name but the browser uses localhost.
    /// The value is the storage endpoint through the account path, before the container name.
    /// </summary>
    public string? EventBannersPublicBaseUrl { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new InvalidOperationException(
                $"Configuration '{SectionName}:ConnectionString' is required when Blob Storage is used.");
        }

        ValidateContainerName(EventBannersContainerName, nameof(EventBannersContainerName));
        ValidateContainerName(ProfilePicturesContainerName, nameof(ProfilePicturesContainerName));

        if (string.Equals(EventBannersContainerName, ProfilePicturesContainerName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Event banners and profile pictures must use separate containers.");
        }

        if (!string.IsNullOrWhiteSpace(EventBannersPublicBaseUrl)
            && (!Uri.TryCreate(EventBannersPublicBaseUrl, UriKind.Absolute, out var publicBaseUri)
                || publicBaseUri.Scheme is not ("http" or "https")
                || !string.IsNullOrEmpty(publicBaseUri.UserInfo)
                || !string.IsNullOrEmpty(publicBaseUri.Query)
                || !string.IsNullOrEmpty(publicBaseUri.Fragment)))
        {
            throw new InvalidOperationException(
                $"Configuration '{SectionName}:EventBannersPublicBaseUrl' must be an absolute HTTP or HTTPS URL without credentials, a query, or a fragment.");
        }
    }

    private static void ValidateContainerName(string? name, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(name)
            || name.Length is < 3 or > 63
            || !Regex.IsMatch(name, "^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException(
                $"Configuration '{SectionName}:{propertyName}' must be a valid lowercase Blob container name.");
        }
    }
}
