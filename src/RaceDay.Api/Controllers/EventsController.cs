using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Authentication;
using RaceDay.Api.Data;
using RaceDay.Api.Data.Entities;

namespace RaceDay.Api.Controllers;

/// <summary>Browses events publicly and lets Organisers manage their own events.</summary>
[ApiController]
[Route("api/events")]
public sealed class EventsController(RaceDayDbContext dbContext) : ControllerBase
{
    /// <summary>Lists events for Participants and Organisers to browse.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EventResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EventResponse>>> GetEvents(CancellationToken cancellationToken)
    {
        var events = await dbContext.Events.AsNoTracking()
            .OrderBy(item => item.EventDate)
            .Select(item => ToResponse(item))
            .ToListAsync(cancellationToken);
        return Ok(events);
    }

    /// <summary>Returns one event, or 404 when its identifier does not exist.</summary>
    [HttpGet("{eventId:int}")]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> GetEvent(int eventId, CancellationToken cancellationToken)
    {
        var raceEvent = await dbContext.Events.AsNoTracking()
            .SingleOrDefaultAsync(item => item.EventID == eventId, cancellationToken);
        return raceEvent is null ? NotFound() : Ok(ToResponse(raceEvent));
    }

    /// <summary>Creates an event owned by the signed-in Organiser.</summary>
    [HttpPost]
    [SessionAuthorize(UserRoles.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EventResponse>> CreateEvent(
        EventWriteRequest request,
        CancellationToken cancellationToken)
    {
        var raceEvent = new RaceEvent
        {
            OrganiserID = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            EventDate = request.EventDate,
            Location = request.Location.Trim(),
            Distance = request.Distance,
            EventType = request.NormalizedEventType
        };

        dbContext.Events.Add(raceEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetEvent), new { eventId = raceEvent.EventID }, ToResponse(raceEvent));
    }

    /// <summary>Updates an event only when it belongs to the signed-in Organiser.</summary>
    [HttpPut("{eventId:int}")]
    [SessionAuthorize(UserRoles.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> UpdateEvent(
        int eventId,
        EventWriteRequest request,
        CancellationToken cancellationToken)
    {
        var raceEvent = await dbContext.Events
            .SingleOrDefaultAsync(item => item.EventID == eventId, cancellationToken);
        if (raceEvent is null)
        {
            return NotFound();
        }

        if (raceEvent.OrganiserID != HttpContext.Session.GetInt32(SessionKeys.UserId))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        raceEvent.Name = request.Name.Trim();
        raceEvent.Description = request.Description?.Trim();
        raceEvent.EventDate = request.EventDate;
        raceEvent.Location = request.Location.Trim();
        raceEvent.Distance = request.Distance;
        raceEvent.EventType = request.NormalizedEventType;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(raceEvent));
    }

    /// <summary>Deletes an owned event only if it has no dependent records.</summary>
    [HttpDelete("{eventId:int}")]
    [SessionAuthorize(UserRoles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteEvent(int eventId, CancellationToken cancellationToken)
    {
        var raceEvent = await dbContext.Events
            .SingleOrDefaultAsync(item => item.EventID == eventId, cancellationToken);
        if (raceEvent is null)
        {
            return NotFound();
        }

        if (raceEvent.OrganiserID != HttpContext.Session.GetInt32(SessionKeys.UserId))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var hasDependents = await dbContext.Categories.AnyAsync(item => item.EventID == eventId, cancellationToken)
            || await dbContext.EventRoutes.AnyAsync(item => item.EventID == eventId, cancellationToken)
            || await dbContext.Enrolments.AnyAsync(item => item.EventID == eventId, cancellationToken);
        if (hasDependents)
        {
            return Conflict(new { message = "The event cannot be deleted while it has categories, routes, or enrolments." });
        }

        dbContext.Events.Remove(raceEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static EventResponse ToResponse(RaceEvent item) => new(
        item.EventID,
        item.Name,
        item.Description,
        item.EventDate,
        item.Location,
        item.Distance,
        item.EventType,
        item.EventBannerUrl);
}
