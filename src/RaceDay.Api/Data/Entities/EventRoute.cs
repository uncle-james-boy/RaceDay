namespace RaceDay.Api.Data.Entities;

public sealed class EventRoute
{
    public int RouteID { get; set; }
    public int EventID { get; set; }
    public required string RouteName { get; set; }
    public decimal Distance { get; set; }
    public string? RouteDescription { get; set; }
    public string? RouteMapUrl { get; set; }

    public RaceEvent? Event { get; set; }
}
