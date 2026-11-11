using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaceDay.Web.Services;

/// <summary>Sends requests to the existing RaceDay API.</summary>
public sealed class RaceDayApiClient(HttpClient httpClient, ApiSessionContext session)
{
    public async Task<ApiLoginResult> LoginAsync<TRequest>(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
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
            var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken: cancellationToken);
            return string.IsNullOrWhiteSpace(error?.Message)
                ? "The API could not sign in with those details."
                : error.Message;
        }
        catch (JsonException)
        {
            return "The API could not sign in with those details.";
        }
    }
}

public sealed record ApiLoginResult(HttpStatusCode StatusCode, ApiUserResponse? User, string? ErrorMessage)
{
    public bool IsSuccess => User is not null && ErrorMessage is null;
}

public sealed record ApiUserResponse(int UserID, string FullName, string Email, string Role, string? ProfilePictureUrl);

public sealed record ApiErrorResponse(string? Message);
