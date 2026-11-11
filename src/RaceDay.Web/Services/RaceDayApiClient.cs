using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaceDay.Web.Services;

/// <summary>Sends requests to the existing RaceDay API.</summary>
public sealed class RaceDayApiClient(HttpClient httpClient, ApiSessionContext session)
{
    public async Task<ApiOperationResult> RegisterAsync<TRequest>(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        using var content = JsonContent.Create(request);
        using var response = await SendCoreAsync(
            HttpMethod.Post,
            "api/auth/register",
            content,
            includeSessionCookie: false,
            cancellationToken);

        return new ApiOperationResult(
            response.StatusCode,
            response.IsSuccessStatusCode ? null : await ReadErrorMessageAsync(response, cancellationToken));
    }

    public async Task<ApiLoginResult> LoginAsync<TRequest>(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        session.Clear();
        using var content = JsonContent.Create(request);
        using var response = await SendCoreAsync(
            HttpMethod.Post,
            "api/auth/login",
            content,
            includeSessionCookie: false,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new ApiLoginResult(response.StatusCode, null, await ReadErrorMessageAsync(response, cancellationToken));
        }

        var user = await response.Content.ReadFromJsonAsync<ApiUserResponse>(cancellationToken: cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException("The API returned an empty login response.");
        }

        var cookie = ReadSessionCookie(response);
        session.SignIn(user, cookie);
        return new ApiLoginResult(response.StatusCode, user, null);
    }

    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string relativeUri,
        HttpContent? content = null,
        CancellationToken cancellationToken = default)
    {
        return await SendCoreAsync(method, relativeUri, content, includeSessionCookie: true, cancellationToken);
    }

    public async Task<HttpResponseMessage> SendUnauthenticatedAsync(
        HttpMethod method,
        string relativeUri,
        HttpContent? content = null,
        CancellationToken cancellationToken = default)
    {
        return await SendCoreAsync(method, relativeUri, content, includeSessionCookie: false, cancellationToken);
    }

    public async Task<ApiOperationResult> LogoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await SendAsync(HttpMethod.Post, "api/auth/logout", cancellationToken: cancellationToken);
            return new ApiOperationResult(
                response.StatusCode,
                response.IsSuccessStatusCode ? null : await ReadErrorMessageAsync(response, cancellationToken));
        }
        finally
        {
            // End the MVC sign-in even when the API is unavailable; the browser must not retain a stale session.
            session.Clear();
        }
    }

    private async Task<HttpResponseMessage> SendCoreAsync(
        HttpMethod method,
        string relativeUri,
        HttpContent? content,
        bool includeSessionCookie,
        CancellationToken cancellationToken)
    {
        ValidateRelativeUri(relativeUri);

        using var request = new HttpRequestMessage(method, relativeUri)
        {
            Content = content
        };

        if (includeSessionCookie && session.ApiSessionCookie is { Length: > 0 } cookie)
        {
            request.Headers.TryAddWithoutValidation(
                "Cookie",
                $"{ApiSessionContext.ApiSessionCookieName}={cookie}");
        }

        var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (includeSessionCookie && response.StatusCode == HttpStatusCode.Unauthorized && session.IsAuthenticated)
        {
            response.Dispose();
            session.Clear();
            throw new ApiSessionExpiredException();
        }

        return response;
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

    private static string ReadSessionCookie(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Set-Cookie", out var headers))
        {
            foreach (var header in headers)
            {
                var pairEnd = header.IndexOf(';');
                var pair = pairEnd < 0 ? header : header[..pairEnd];
                var separator = pair.IndexOf('=');
                if (separator <= 0
                    || !string.Equals(pair[..separator].Trim(), ApiSessionContext.ApiSessionCookieName, StringComparison.Ordinal))
                {
                    continue;
                }

                var value = pair[(separator + 1)..].Trim();
                if (ApiSessionContext.IsValidApiCookieValue(value))
                {
                    return value;
                }

                throw new InvalidOperationException("The API returned an invalid session cookie.");
            }
        }

        throw new InvalidOperationException("The API login succeeded but did not set its session cookie.");
    }

    private static async Task<string> ReadErrorMessageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            var root = document.RootElement;
            if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(message.GetString()))
            {
                return message.GetString()!;
            }

            if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(title.GetString()))
            {
                return title.GetString()!;
            }

            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var firstError = errors.EnumerateObject()
                    .SelectMany(property => property.Value.ValueKind == JsonValueKind.Array
                        ? property.Value.EnumerateArray().Select(item => item.GetString())
                        : Enumerable.Empty<string?>())
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
                if (firstError is not null)
                {
                    return firstError;
                }
            }

            return "The request could not be completed. Check the details and try again.";
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return "The request could not be completed. Check the details and try again.";
        }
    }
}

public sealed record ApiLoginResult(HttpStatusCode StatusCode, ApiUserResponse? User, string? ErrorMessage)
{
    public bool IsSuccess => User is not null && ErrorMessage is null;
}

public sealed record ApiOperationResult(HttpStatusCode StatusCode, string? ErrorMessage)
{
    public bool IsSuccess => (int)StatusCode is >= 200 and < 300 && ErrorMessage is null;
}

public sealed class ApiSessionExpiredException() : Exception("Your RaceDay session has expired.");

public sealed record ApiUserResponse(int UserID, string FullName, string Email, string Role, string? ProfilePictureUrl);
