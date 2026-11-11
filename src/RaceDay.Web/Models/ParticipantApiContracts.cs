using System.ComponentModel.DataAnnotations;

namespace RaceDay.Web.Models;

public sealed record EventCard(int EventID, string Name, string? Description, DateTime EventDate,
    string Location, decimal Distance, string EventType, string? EventBannerUrl);

public sealed record EventCategory(int CategoryID, int EventID, string CategoryName,
    int? MinAge, int? MaxAge, decimal? Distance);

public sealed record EventRoute(int RouteID, int EventID, string RouteName,
    decimal Distance, string? RouteDescription, string? RouteMapUrl);

public sealed record ParticipantEnrolment(int EnrolmentID, int EventID, string EventName,
    int CategoryID, string CategoryName, int ParticipantID, string ParticipantName,
    DateTime EnrolmentDate, string Status);

public sealed record ParticipantRaceResult(int ResultID, int EnrolmentID, int EventID,
    string EventName, DateTime EventDate, string CategoryName, TimeSpan FinishTime,
    int Position, DateTime CapturedAt);

public sealed record ParticipantProfile(int UserID, string FullName, string Email,
    string Role, string? ProfilePictureUrl, DateTime CreatedAt);

public sealed class ParticipantHomeViewModel
{
    public string? Search { get; set; }
    public string? EventType { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public IReadOnlyList<EventCard> Events { get; set; } = [];
    public string? ErrorMessage { get; set; }
}

public sealed class EventDetailsViewModel
{
    public EventCard? Event { get; set; }
    public IReadOnlyList<EventCategory> Categories { get; set; } = [];
    public IReadOnlyList<EventRoute> Routes { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
}

public sealed class ParticipantListViewModel<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
}

public sealed class ParticipantProfileViewModel
{
    public ParticipantProfile? Profile { get; set; }
    public UpdateParticipantProfileModel Edit { get; set; } = new();
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class UpdateParticipantProfileModel
{
    [Required, StringLength(100, MinimumLength = 2)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [StringLength(500), Url]
    [Display(Name = "Profile picture URL")]
    public string? ProfilePictureUrl { get; set; }
}

public sealed class EnrolParticipantModel
{
    [Range(1, int.MaxValue)]
    public int EventID { get; set; }

    [Range(1, int.MaxValue)]
    [Display(Name = "Category")]
    public int CategoryID { get; set; }
}
