using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Authentication;
using RaceDay.Api.Data;
using RaceDay.Api.Data.Entities;

namespace RaceDay.Api.Controllers;

/// <summary>Lists event categories publicly and manages them for the owning Organiser.</summary>
[ApiController]
public sealed class CategoriesController(RaceDayDbContext dbContext) : ControllerBase
{
    /// <summary>Lists the categories attached to an event.</summary>
    [HttpGet("api/events/{eventId:int}/categories")]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> GetForEvent(
        int eventId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Events.AnyAsync(item => item.EventID == eventId, cancellationToken))
        {
            return NotFound();
        }

        var categories = await dbContext.Categories.AsNoTracking()
            .Where(item => item.EventID == eventId)
            .OrderBy(item => item.CategoryName)
            .Select(item => new CategoryResponse(item.CategoryID, item.EventID, item.CategoryName,
                item.MinAge, item.MaxAge, item.Distance))
            .ToListAsync(cancellationToken);
        return Ok(categories);
    }

    /// <summary>Adds a category to an event owned by the signed-in Organiser.</summary>
    [HttpPost("api/events/{eventId:int}/categories")]
    [SessionAuthorize(UserRoles.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryResponse>> Create(
        int eventId,
        CategoryWriteRequest request,
        CancellationToken cancellationToken)
    {
        var eventCheck = await CheckEventOwner(eventId, cancellationToken);
        if (eventCheck is not null)
        {
            return eventCheck;
        }

        var category = new Category
        {
            EventID = eventId,
            CategoryName = request.CategoryName.Trim(),
            MinAge = request.MinAge,
            MaxAge = request.MaxAge,
            Distance = request.Distance
        };
        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);
        var response = ToResponse(category);
        return Created($"/api/events/{eventId}/categories", response);
    }

    /// <summary>Updates a category under an event owned by the signed-in Organiser.</summary>
    [HttpPut("api/categories/{categoryId:int}")]
    [SessionAuthorize(UserRoles.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryResponse>> Update(
        int categoryId,
        CategoryWriteRequest request,
        CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories
            .SingleOrDefaultAsync(item => item.CategoryID == categoryId, cancellationToken);
        if (category is null)
        {
            return NotFound();
        }

        var eventCheck = await CheckEventOwner(category.EventID, cancellationToken);
        if (eventCheck is not null)
        {
            return eventCheck;
        }

        Apply(category, request);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(category));
    }

    /// <summary>Deletes a category only when nobody has enrolled in it.</summary>
    [HttpDelete("api/categories/{categoryId:int}")]
    [SessionAuthorize(UserRoles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int categoryId, CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories
            .SingleOrDefaultAsync(item => item.CategoryID == categoryId, cancellationToken);
        if (category is null)
        {
            return NotFound();
        }

        var eventCheck = await CheckEventOwner(category.EventID, cancellationToken);
        if (eventCheck is not null)
        {
            return eventCheck;
        }

        if (await dbContext.Enrolments.AnyAsync(item => item.CategoryID == categoryId, cancellationToken))
        {
            return Conflict(new { message = "The category cannot be deleted while enrolments refer to it." });
        }

        dbContext.Categories.Remove(category);
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

    private static void Apply(Category category, CategoryWriteRequest request)
    {
        category.CategoryName = request.CategoryName.Trim();
        category.MinAge = request.MinAge;
        category.MaxAge = request.MaxAge;
        category.Distance = request.Distance;
    }

    private static CategoryResponse ToResponse(Category category) =>
        new(category.CategoryID, category.EventID, category.CategoryName,
            category.MinAge, category.MaxAge, category.Distance);
}

/// <summary>The fields callers can set for an event category.</summary>
public sealed class CategoryWriteRequest : IValidatableObject
{
    [Required, StringLength(100)]
    public string CategoryName { get; init; } = string.Empty;

    [Range(0, 150)]
    public int? MinAge { get; init; }

    [Range(0, 150)]
    public int? MaxAge { get; init; }

    public decimal? Distance { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinAge.HasValue && MaxAge.HasValue && MinAge > MaxAge)
        {
            yield return new ValidationResult("MinAge cannot be greater than MaxAge.", [nameof(MinAge), nameof(MaxAge)]);
        }

        if (Distance is <= 0 or > 999.99m)
        {
            yield return new ValidationResult("Distance must be greater than zero and no more than 999.99 km.", [nameof(Distance)]);
        }
    }
}

/// <summary>Category information returned to API clients.</summary>
public sealed record CategoryResponse(
    int CategoryID,
    int EventID,
    string CategoryName,
    int? MinAge,
    int? MaxAge,
    decimal? Distance);
