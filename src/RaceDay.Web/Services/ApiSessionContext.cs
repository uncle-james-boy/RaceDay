namespace RaceDay.Web.Services;

/// <summary>Stores one browser user's API session and identity in the server-side MVC session.</summary>
public sealed class ApiSessionContext(Microsoft.AspNetCore.Http.ISession currentSession)
{
    public const string ApiSessionCookieName = ".RaceDay.Session";

    private const string ApiCookieKey = "RaceDay.ApiCookie";
    private const string UserIdKey = "RaceDay.UserId";
    private const string UserNameKey = "RaceDay.UserName";
    private const string RoleKey = "RaceDay.Role";

    public string? ApiSessionCookie => currentSession.GetString(ApiCookieKey);
    public int? UserId => currentSession.GetInt32(UserIdKey);
    public string? FullName => currentSession.GetString(UserNameKey);
    public string? Role => currentSession.GetString(RoleKey);
    public bool IsAuthenticated => UserId.HasValue && ApiSessionCookie is not null;

    public void SignIn(ApiUserResponse user, string apiSessionCookie)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.UserID <= 0 || string.IsNullOrWhiteSpace(user.FullName)
            || user.Role is not "Organiser" and not "Participant"
            || !IsValidApiCookieValue(apiSessionCookie))
        {
            throw new ArgumentException("The API login response did not contain a valid user session.", nameof(user));
        }

        currentSession.Clear();
        currentSession.SetString(ApiCookieKey, apiSessionCookie);
        currentSession.SetInt32(UserIdKey, user.UserID);
        currentSession.SetString(UserNameKey, user.FullName);
        currentSession.SetString(RoleKey, user.Role);
    }

    public void Clear() => currentSession.Clear();

    internal static bool IsValidApiCookieValue(string value) =>
        value.Length is > 0 and <= 512
        && value.All(character => IsCookieOctet(character));

    internal static bool IsCookieOctet(int codePoint) =>
        codePoint == 0x21
        || codePoint is >= 0x23 and <= 0x2B
        || codePoint is >= 0x2D and <= 0x3A
        || codePoint is >= 0x3C and <= 0x5B
        || codePoint is >= 0x5D and <= 0x7E;
}
