namespace CourtSpotterMcpServer.Models;

public class CourtAvailabilitiesResponse
{
    public int TotalCount { get; set; }
    public List<CourtAvailability> CourtAvailabilities { get; set; } = [];
}

public enum CourtType
{
    Indoor,
    Outdoor,
}

public class CourtAvailability
{
    public string Id { get; set; }

    public string ClubId { get; set; }

    public string ClubName { get; set; }

    public string CourtName { get; set; }
    
    public DateTime DateTime { get; set; }
    
    public decimal Price { get; set; }

    public string BookingUrl { get; set; }
    
    public string Provider { get; set; }
    
    public int DurationInMinutes { get; set; }
    
    public CourtType CourtType { get; set; } = CourtType.Indoor;
}