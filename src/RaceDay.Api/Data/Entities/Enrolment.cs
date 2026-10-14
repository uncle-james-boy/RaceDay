namespace RaceDay.Api.Data.Entities;

public sealed class Enrolment
{
    public int EnrolmentID { get; set; }
    public int EventID { get; set; }
    public int ParticipantID { get; set; }
    public int CategoryID { get; set; }
    public DateTime EnrolmentDate { get; set; }
    public required string Status { get; set; }

    public RaceEvent? Event { get; set; }
    public User? Participant { get; set; }
    public Category? Category { get; set; }
    public RaceResult? Result { get; set; }
}
