namespace RaceDay.Api.Data.Entities;

public sealed class RaceEvent
{
    public int EventID { get; set; }
    public int OrganiserID { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }
    public required string Location { get; set; }
    public decimal Distance { get; set; }
    public required string EventType { get; set; }
    public string? EventBannerUrl { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? Organiser { get; set; }
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<EventRoute> Routes { get; set; } = new List<EventRoute>();
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}
