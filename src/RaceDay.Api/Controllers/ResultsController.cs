using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Authentication;
using RaceDay.Api.Data;
using RaceDay.Api.Data.Entities;

namespace RaceDay.Api.Controllers;

/// <summary>Captures results for completed events and lists each Participant's own history.</summary>
[ApiController]
[Route("api/results")]
public sealed class ResultsController(RaceDayDbContext dbContext) : ControllerBase
{
    /// <summary>Records a result for an enrolment in the Organiser's completed event.</summary>
    [HttpPost]
    [SessionAuthorize(UserRoles.Organiser)]
    [ProducesResponseType(typeof(RaceResultResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RaceResultResponse>> Create(
        CreateRaceResultRequest request,
        CancellationToken cancellationToken)
    {
        var enrolment = await dbContext.Enrolments.AsNoTracking()
            .Where(item => item.EnrolmentID == request.EnrolmentID)
            .Select(item => new
            {
                item.EnrolmentID,
                item.EventID,
                EventDate = item.Event!.EventDate,
                item.Event.OrganiserID
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (enrolment is null)
        {
            return NotFound(new { message = "The enrolment was not found." });
        }

        if (enrolment.OrganiserID != HttpContext.Session.GetInt32(SessionKeys.UserId))
        {
            return Forbid();
        }

        if (enrolment.EventDate >= DateTime.Now)
        {
            return BadRequest(new { message = "Results can only be recorded after the event has finished." });
        }

        if (await dbContext.Results.AnyAsync(item => item.EnrolmentID == request.EnrolmentID, cancellationToken))
        {
            return Conflict(new { message = "A result has already been recorded for this enrolment." });
        }

        var result = new RaceResult
        {
            EnrolmentID = enrolment.EnrolmentID,
            FinishTime = request.FinishTime,
            Position = request.Position,
            CapturedAt = DateTime.UtcNow
        };
        dbContext.Results.Add(result);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await dbContext.Results.AsNoTracking()
            .Where(item => item.ResultID == result.ResultID)
            .Select(ToResponse())
            .SingleAsync(cancellationToken);
        return Created("/api/results", response);
    }

    /// <summary>Lists results belonging only to the Participant in the current session.</summary>
    [HttpGet]
    [SessionAuthorize(UserRoles.Participant)]
    [ProducesResponseType(typeof(IReadOnlyList<RaceResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<RaceResultResponse>>> GetMine(CancellationToken cancellationToken)
    {
        var participantId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
        var results = await dbContext.Results.AsNoTracking()
            .Where(item => item.Enrolment!.ParticipantID == participantId)
            .OrderByDescending(item => item.Enrolment!.Event!.EventDate)
            .Select(ToResponse())
            .ToListAsync(cancellationToken);
        return Ok(results);
    }

    private static System.Linq.Expressions.Expression<Func<RaceResult, RaceResultResponse>> ToResponse() => item =>
        new RaceResultResponse(
            item.ResultID,
            item.EnrolmentID,
            item.Enrolment!.EventID,
            item.Enrolment.Event!.Name,
            item.Enrolment.Event.EventDate,
            item.Enrolment.Category!.CategoryName,
            item.FinishTime,
            item.Position,
            item.CapturedAt);
}

/// <summary>Finish time and place submitted by the owning Organiser.</summary>
public sealed class CreateRaceResultRequest : IValidatableObject
{
    [Range(1, int.MaxValue)]
    public int EnrolmentID { get; init; }

    public TimeSpan FinishTime { get; init; }

    [Range(1, int.MaxValue)]
    public int Position { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FinishTime <= TimeSpan.Zero)
        {
            yield return new ValidationResult("FinishTime must be greater than zero.", [nameof(FinishTime)]);
        }
    }
}

/// <summary>Race result details shown to the Participant.</summary>
public sealed record RaceResultResponse(
    int ResultID,
    int EnrolmentID,
    int EventID,
    string EventName,
    DateTime EventDate,
    string CategoryName,
    TimeSpan FinishTime,
    int Position,
    DateTime CapturedAt);
