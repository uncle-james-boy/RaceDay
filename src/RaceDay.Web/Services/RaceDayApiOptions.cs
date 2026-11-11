namespace RaceDay.Web.Services;

/// <summary>Settings for the existing RaceDay API.</summary>
public sealed class RaceDayApiOptions
{
    public const string SectionName = "RaceDayApi";

    public string BaseUrl { get; set; } = "https://localhost:7107/";

    public Uri GetBaseAddress()
    {
        if (!TryGetBaseAddress(out var address))
        {
            throw new InvalidOperationException(
                "RaceDayApi:BaseUrl must be an absolute HTTP or HTTPS URL without credentials, a query, or a fragment.");
        }

        return address;
    }

    public bool TryGetBaseAddress(out Uri address)
    {
        address = null!;
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(parsed.UserInfo)
            || !string.IsNullOrEmpty(parsed.Query)
            || !string.IsNullOrEmpty(parsed.Fragment))
        {
            return false;
        }

        address = new Uri(parsed.AbsoluteUri.EndsWith('/') ? parsed.AbsoluteUri : parsed.AbsoluteUri + '/', UriKind.Absolute);
        return true;
    }
}
