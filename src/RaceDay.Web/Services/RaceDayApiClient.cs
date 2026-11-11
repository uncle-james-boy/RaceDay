namespace RaceDay.Web.Services;

/// <summary>Sends requests to the existing RaceDay API.</summary>
public sealed class RaceDayApiClient(HttpClient httpClient)
{
    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string relativeUri,
        HttpContent? content = null,
        CancellationToken cancellationToken = default)
    {
        ValidateRelativeUri(relativeUri);

        using var request = new HttpRequestMessage(method, relativeUri)
        {
            Content = content
        };

        return await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
    }

    private static void ValidateRelativeUri(string relativeUri)
    {
        if (string.IsNullOrWhiteSpace(relativeUri)
            || relativeUri.StartsWith("/", StringComparison.Ordinal)
            || relativeUri.StartsWith('\\')
            || relativeUri.Contains('\\')
            || relativeUri.Split('/').Any(segment => segment == "..")
            || Uri.TryCreate(relativeUri, UriKind.Absolute, out _))
        {
            throw new ArgumentException("API request paths must be safe, relative paths.", nameof(relativeUri));
        }
    }
}
