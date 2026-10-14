using Microsoft.OpenApi;
using RaceDay.Api.Authentication;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RaceDay.Api.Authentication;

/// <summary>Adds the server-side session cookie requirement to protected Swagger operations.</summary>
public sealed class SessionCookieOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var requiresSession = context.MethodInfo.IsDefined(typeof(SessionAuthorizeAttribute), inherit: true)
            || context.MethodInfo.DeclaringType?.IsDefined(typeof(SessionAuthorizeAttribute), inherit: true) == true;
        if (!requiresSession)
        {
            return;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("SessionCookie", null, null)] = new List<string>()
        });
    }
}
