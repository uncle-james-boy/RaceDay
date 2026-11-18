using RaceDay.Web.Services;

namespace RaceDay.Web.Security;

/// <summary>Turns an expired API session into a clear MVC sign-in redirect.</summary>
public sealed class ApiSessionExpiredMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ApiSessionExpiredException) when (!context.Response.HasStarted)
        {
            context.Response.Clear();
            context.Response.Redirect("/Auth/Login?sessionExpired=true");
        }
    }
}
