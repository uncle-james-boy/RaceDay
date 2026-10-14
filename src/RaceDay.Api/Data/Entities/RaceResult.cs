namespace RaceDay.Api.Data.Entities;

public sealed class RaceResult
{
    public int ResultID { get; set; }
    public int EnrolmentID { get; set; }
    public TimeSpan FinishTime { get; set; }
    public int Position { get; set; }
    public DateTime CapturedAt { get; set; }

    public Enrolment? Enrolment { get; set; }
}
