using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RaceDay.Web.Security;

/// <summary>Requires a valid API session, and optionally a matching RaceDay role.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RaceDayAuthorizeAttribute(string? requiredRole = null) : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        await context.HttpContext.Session.LoadAsync(context.HttpContext.RequestAborted);
        var userId = context.HttpContext.Session.GetInt32("RaceDay.UserId");
        var apiCookie = context.HttpContext.Session.GetString("RaceDay.ApiCookie");
        var role = context.HttpContext.Session.GetString("RaceDay.Role");

        if (!userId.HasValue || string.IsNullOrWhiteSpace(apiCookie))
        {
            var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
            context.Result = new RedirectToActionResult("Login", "Auth", new { returnUrl });
            return;
        }

        if (requiredRole is not null && !string.Equals(role, requiredRole, StringComparison.Ordinal))
        {
            context.Result = new ViewResult
            {
                ViewName = "~/Views/Auth/AccessDenied.cshtml",
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
