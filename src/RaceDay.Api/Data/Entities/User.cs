namespace RaceDay.Api.Data.Entities;

public sealed class User
{
    public int UserID { get; set; }
    public required string FullName { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string Role { get; set; }
    // Stores a generated private Blob name (not a public URL); the existing column remains unchanged.
    public string? ProfilePictureUrl { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<RaceEvent> OrganisedEvents { get; set; } = new List<RaceEvent>();
    public ICollection<Enrolment> ParticipantEnrolments { get; set; } = new List<Enrolment>();
}
