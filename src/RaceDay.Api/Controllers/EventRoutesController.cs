using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Authentication;
using RaceDay.Api.Data;
using RaceDay.Api.Data.Entities;

namespace RaceDay.Api.Controllers;

/// <summary>Lists event course routes publicly and lets the owner manage them.</summary>
[ApiController]
public sealed class EventRoutesController(RaceDayDbContext dbContext) : ControllerBase
{
    /// <summary>Lists course routes for an event.</summary>
    [HttpGet("api/events/{eventId:int}/routes")]
    [ProducesResponseType(typeof(IReadOnlyList<EventRouteResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<EventRouteResponse>>> GetForEvent(
        int eventId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Events.AnyAsync(item => item.EventID == eventId, cancellationToken))
        {
            return NotFound();
        }

        var routes = await dbContext.EventRoutes.AsNoTracking()
            .Where(item => item.EventID == eventId)
            .OrderBy(item => item.RouteName)
            .Select(item => new EventRouteResponse(item.RouteID, item.EventID, item.RouteName,
                item.Distance, item.RouteDescription, item.RouteMapUrl))
            .ToListAsync(cancellationToken);
        return Ok(routes);
    }

    /// <summary>Adds a route to an event owned by the signed-in Organiser.</summary>
    [HttpPost("api/events/{eventId:int}/routes")]
    [SessionAuthorize(UserRoles.Organiser)]
    [ProducesResponseType(typeof(EventRouteResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EventRouteResponse>> Create(
        int eventId,
        EventRouteWriteRequest request,
        CancellationToken cancellationToken)
    {
        var eventCheck = await CheckEventOwner(eventId, cancellationToken);
        if (eventCheck is not null)
        {
            return eventCheck;
        }

        if (await dbContext.EventRoutes.AnyAsync(
                item => item.EventID == eventId && item.RouteName == request.RouteName.Trim(), cancellationToken))
        {
            return Conflict(new { message = "A route with this name already exists for the event." });
        }

        var route = new EventRoute
        {
            EventID = eventId,
            RouteName = request.RouteName.Trim(),
            Distance = request.Distance,
            RouteDescription = request.RouteDescription?.Trim(),
            RouteMapUrl = string.IsNullOrWhiteSpace(request.RouteMapUrl) ? null : request.RouteMapUrl.Trim()
        };
        dbContext.EventRoutes.Add(route);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Created($"/api/events/{eventId}/routes", ToResponse(route));
    }

    /// <summary>Updates a route under an event owned by the signed-in Organiser.</summary>
    [HttpPut("api/routes/{routeId:int}")]
    [SessionAuthorize(UserRoles.Organiser)]
    [ProducesResponseType(typeof(EventRouteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EventRouteResponse>> Update(
        int routeId,
        EventRouteWriteRequest request,
        CancellationToken cancellationToken)
    {
        var route = await dbContext.EventRoutes
            .SingleOrDefaultAsync(item => item.RouteID == routeId, cancellationToken);
        if (route is null)
        {
            return NotFound();
        }

        var eventCheck = await CheckEventOwner(route.EventID, cancellationToken);
        if (eventCheck is not null)
        {
            return eventCheck;
        }

        if (await dbContext.EventRoutes.AnyAsync(item => item.EventID == route.EventID
            && item.RouteID != routeId && item.RouteName == request.RouteName.Trim(), cancellationToken))
        {
            return Conflict(new { message = "A route with this name already exists for the event." });
        }

        Apply(route, request);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(route));
    }

    /// <summary>Deletes a route when it belongs to the signed-in Organiser.</summary>
    [HttpDelete("api/routes/{routeId:int}")]
    [SessionAuthorize(UserRoles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int routeId, CancellationToken cancellationToken)
    {
        var route = await dbContext.EventRoutes
            .SingleOrDefaultAsync(item => item.RouteID == routeId, cancellationToken);
        if (route is null)
        {
            return NotFound();
        }

        var eventCheck = await CheckEventOwner(route.EventID, cancellationToken);
        if (eventCheck is not null)
        {
            return eventCheck;
        }

        dbContext.EventRoutes.Remove(route);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<ActionResult?> CheckEventOwner(int eventId, CancellationToken cancellationToken)
    {
        var raceEvent = await dbContext.Events.AsNoTracking()
            .SingleOrDefaultAsync(item => item.EventID == eventId, cancellationToken);
        if (raceEvent is null)
        {
            return NotFound();
        }

        return raceEvent.OrganiserID == HttpContext.Session.GetInt32(SessionKeys.UserId)
            ? null
            : Forbid();
    }

    private static void Apply(EventRoute route, EventRouteWriteRequest request)
    {
        route.RouteName = request.RouteName.Trim();
        route.Distance = request.Distance;
        route.RouteDescription = request.RouteDescription?.Trim();
        route.RouteMapUrl = string.IsNullOrWhiteSpace(request.RouteMapUrl) ? null : request.RouteMapUrl.Trim();
    }

    private static EventRouteResponse ToResponse(EventRoute item) => new(
        item.RouteID, item.EventID, item.RouteName, item.Distance, item.RouteDescription, item.RouteMapUrl);
}

/// <summary>Fields callers can set for an event course route.</summary>
public sealed class EventRouteWriteRequest : IValidatableObject
{
    [Required, StringLength(100)]
    public string RouteName { get; init; } = string.Empty;

    public decimal Distance { get; init; }

    public string? RouteDescription { get; init; }

    [StringLength(500), Url]
    public string? RouteMapUrl { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(RouteName))
        {
            yield return new ValidationResult("RouteName is required.", [nameof(RouteName)]);
        }

        if (Distance <= 0 || Distance > 999.99m)
        {
            yield return new ValidationResult("Distance must be greater than zero and no more than 999.99 km.", [nameof(Distance)]);
        }
    }
}

/// <summary>Route information returned to API clients.</summary>
public sealed record EventRouteResponse(
    int RouteID,
    int EventID,
    string RouteName,
    decimal Distance,
    string? RouteDescription,
    string? RouteMapUrl);
