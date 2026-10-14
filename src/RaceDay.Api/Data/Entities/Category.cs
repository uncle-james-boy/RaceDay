namespace RaceDay.Api.Data.Entities;

public sealed class Category
{
    public int CategoryID { get; set; }
    public int EventID { get; set; }
    public required string CategoryName { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public decimal? Distance { get; set; }

    public RaceEvent? Event { get; set; }
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}
