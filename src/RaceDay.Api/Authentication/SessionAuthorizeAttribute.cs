using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RaceDay.Api.Authentication;

/// <summary>Requires a logged-in session, and optionally a specific role.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SessionAuthorizeAttribute(string? requiredRole = null) : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;
        if (session.GetInt32(SessionKeys.UserId) is null || session.GetString(SessionKeys.Role) is null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (requiredRole is not null && session.GetString(SessionKeys.Role) != requiredRole)
        {
            context.Result = new ForbidResult();
        }
    }
}
