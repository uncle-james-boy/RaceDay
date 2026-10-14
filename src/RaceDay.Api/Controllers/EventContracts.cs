using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Controllers;

/// <summary>The fields callers may set when creating or updating an event.</summary>
public sealed class EventWriteRequest : IValidatableObject
{
    [Required, StringLength(150)]
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public DateTime EventDate { get; init; }

    [Required, StringLength(200)]
    public string Location { get; init; } = string.Empty;

    public decimal Distance { get; init; }

    [Required, StringLength(20)]
    public string EventType { get; init; } = string.Empty;

    internal string NormalizedEventType => EventType.Trim().ToLowerInvariant() switch
    {
        "run" => "Run",
        "walk" => "Walk",
        "cycle" => "Cycle",
        _ => EventType.Trim()
    };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            yield return new ValidationResult("Name is required.", [nameof(Name)]);
        }

        if (string.IsNullOrWhiteSpace(Location))
        {
            yield return new ValidationResult("Location is required.", [nameof(Location)]);
        }

        if (EventDate == default)
        {
            yield return new ValidationResult("EventDate must be a valid date.", [nameof(EventDate)]);
        }

        if (Distance <= 0 || Distance > 999.99m)
        {
            yield return new ValidationResult("Distance must be greater than zero and no more than 999.99 km.", [nameof(Distance)]);
        }

        if (EventType.Trim().ToLowerInvariant() is not "run" and not "walk" and not "cycle")
        {
            yield return new ValidationResult("EventType must be Run, Walk, or Cycle.", [nameof(EventType)]);
        }
    }
}

/// <summary>Public event details returned by the API.</summary>
public sealed record EventResponse(
    int EventID,
    string Name,
    string? Description,
    DateTime EventDate,
    string Location,
    decimal Distance,
    string EventType,
    string? EventBannerUrl);
