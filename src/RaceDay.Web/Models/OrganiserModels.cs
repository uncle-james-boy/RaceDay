using System.ComponentModel.DataAnnotations;

namespace RaceDay.Web.Models;

public sealed record OrganiserEvent(int EventID, string Name, string? Description, DateTime EventDate,
    string Location, decimal Distance, string EventType, string? EventBannerUrl, int EnrolmentCount);

public sealed class OrganiserDashboardViewModel
{
    public IReadOnlyList<OrganiserEvent> Events { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
}

public sealed class EventEditModel
{
    public int? EventID { get; set; }

    [Required, StringLength(150, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required, Display(Name = "Event date and time")]
    public DateTime EventDate { get; set; }

    [Required, StringLength(200)]
    public string Location { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "999.99")]
    public decimal Distance { get; set; }

    [Required]
    public string EventType { get; set; } = "Run";

    public string? EventBannerUrl { get; set; }
}

public sealed class EventFormViewModel
{
    public EventEditModel Event { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public sealed class EventDeleteViewModel
{
    public int EventID { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed record OrganiserCategory(int CategoryID, int EventID, string CategoryName,
    int? MinAge, int? MaxAge, decimal? Distance);

public sealed class CategoryEditModel
{
    public int? CategoryID { get; set; }
    public int EventID { get; set; }

    [Required, StringLength(100, MinimumLength = 2)]
    [Display(Name = "Category name")]
    public string CategoryName { get; set; } = string.Empty;

    [Range(0, 150)]
    public int? MinAge { get; set; }

    [Range(0, 150)]
    public int? MaxAge { get; set; }

    [Range(typeof(decimal), "0.01", "999.99")]
    public decimal? Distance { get; set; }
}

public sealed class OrganiserCategoriesViewModel
{
    public int EventID { get; set; }
    public string EventName { get; set; } = string.Empty;
    public IReadOnlyList<OrganiserCategory> Categories { get; set; } = [];
    public CategoryEditModel Edit { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
}

public sealed record OrganiserEnrolment(int EnrolmentID, int EventID, string EventName,
    int CategoryID, string CategoryName, int ParticipantID, string ParticipantName,
    DateTime EnrolmentDate, string Status);

public sealed class OrganiserRosterViewModel
{
    public int EventID { get; set; }
    public string EventName { get; set; } = string.Empty;
    public IReadOnlyList<OrganiserEnrolment> Enrolments { get; set; } = [];
    public IReadOnlyList<OrganiserRaceResult> Results { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
}

public sealed record OrganiserRaceResult(int ResultID, int EnrolmentID, int EventID,
    string EventName, DateTime EventDate, string CategoryName, TimeSpan FinishTime,
    int Position, DateTime CapturedAt);

public sealed class CreateResultModel
{
    [Range(1, int.MaxValue)]
    public int EnrolmentID { get; set; }

    [Required]
    [RegularExpression("^\\d{1,3}:\\d{2}:\\d{2}$", ErrorMessage = "Enter a time as HH:MM:SS.")]
    [Display(Name = "Finish time (HH:MM:SS)")]
    public string FinishTime { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Position { get; set; }
}
