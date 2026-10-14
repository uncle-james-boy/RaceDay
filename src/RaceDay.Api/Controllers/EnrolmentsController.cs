using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Authentication;
using RaceDay.Api.Data;
using RaceDay.Api.Data.Entities;

namespace RaceDay.Api.Controllers;

/// <summary>Enrols Participants and exposes only records they or the event owner may see.</summary>
[ApiController]
[Route("api/enrolments")]
public sealed class EnrolmentsController(RaceDayDbContext dbContext) : ControllerBase
{
    /// <summary>Creates an enrolment for the Participant in the current session.</summary>
    [HttpPost]
    [SessionAuthorize(UserRoles.Participant)]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrolmentResponse>> Create(
        CreateEnrolmentRequest request,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Events.AnyAsync(item => item.EventID == request.EventID, cancellationToken))
        {
            return NotFound(new { message = "The event was not found." });
        }

        var categoryEventId = await dbContext.Categories.AsNoTracking()
            .Where(item => item.CategoryID == request.CategoryID)
            .Select(item => (int?)item.EventID)
            .SingleOrDefaultAsync(cancellationToken);
        if (categoryEventId is null)
        {
            return NotFound(new { message = "The category was not found." });
        }

        if (categoryEventId.Value != request.EventID)
        {
            return BadRequest(new { message = "The selected category does not belong to the selected event." });
        }

        var participantId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
        if (await dbContext.Enrolments.AnyAsync(item => item.ParticipantID == participantId
            && item.EventID == request.EventID && item.CategoryID == request.CategoryID, cancellationToken))
        {
            return Conflict(new { message = "You are already enrolled in this event category." });
        }

        var enrolment = new Enrolment
        {
            EventID = request.EventID,
            CategoryID = request.CategoryID,
            ParticipantID = participantId,
            EnrolmentDate = DateTime.UtcNow,
            Status = "Pending"
        };
        dbContext.Enrolments.Add(enrolment);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await dbContext.Enrolments.AsNoTracking()
            .Where(item => item.EnrolmentID == enrolment.EnrolmentID)
            .Select(ToResponse())
            .SingleAsync(cancellationToken);
        return Created("/api/enrolments", response);
    }

    /// <summary>Lists only enrolments owned by the Participant in the current session.</summary>
    [HttpGet]
    [SessionAuthorize(UserRoles.Participant)]
    [ProducesResponseType(typeof(IReadOnlyList<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<EnrolmentResponse>>> GetMine(CancellationToken cancellationToken)
    {
        var participantId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
        var enrolments = await dbContext.Enrolments.AsNoTracking()
            .Where(item => item.ParticipantID == participantId)
            .OrderByDescending(item => item.EnrolmentDate)
            .Select(ToResponse())
            .ToListAsync(cancellationToken);
        return Ok(enrolments);
    }

    /// <summary>Lists enrolments for an event owned by the signed-in Organiser.</summary>
    [HttpGet("/api/events/{eventId:int}/enrolments")]
    [SessionAuthorize(UserRoles.Organiser)]
    [ProducesResponseType(typeof(IReadOnlyList<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<EnrolmentResponse>>> GetForEvent(
        int eventId,
        CancellationToken cancellationToken)
    {
        var raceEvent = await dbContext.Events.AsNoTracking()
            .SingleOrDefaultAsync(item => item.EventID == eventId, cancellationToken);
        if (raceEvent is null)
        {
            return NotFound();
        }

        if (raceEvent.OrganiserID != HttpContext.Session.GetInt32(SessionKeys.UserId))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var enrolments = await dbContext.Enrolments.AsNoTracking()
            .Where(item => item.EventID == eventId)
            .OrderBy(item => item.EnrolmentDate)
            .Select(ToResponse())
            .ToListAsync(cancellationToken);
        return Ok(enrolments);
    }

    private static System.Linq.Expressions.Expression<Func<Enrolment, EnrolmentResponse>> ToResponse() => item =>
        new EnrolmentResponse(
            item.EnrolmentID,
            item.EventID,
            item.Event!.Name,
            item.CategoryID,
            item.Category!.CategoryName,
            item.ParticipantID,
            item.Participant!.FullName,
            item.EnrolmentDate,
            item.Status);
}

/// <summary>The event and category requested for an enrolment.</summary>
public sealed class CreateEnrolmentRequest
{
    [Range(1, int.MaxValue)]
    public int EventID { get; init; }

    [Range(1, int.MaxValue)]
    public int CategoryID { get; init; }
}

/// <summary>Enrolment details returned to the Participant or owning Organiser.</summary>
public sealed record EnrolmentResponse(
    int EnrolmentID,
    int EventID,
    string EventName,
    int CategoryID,
    string CategoryName,
    int ParticipantID,
    string ParticipantName,
    DateTime EnrolmentDate,
    string Status);
