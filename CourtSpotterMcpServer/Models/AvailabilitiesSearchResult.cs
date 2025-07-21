namespace CourtSpotterMcpServer.Models;

public class AvailabilitiesSearchResult
{
    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }
    
    public List<CourtAvailabilityDto> CourtAvailabilities { get; set; } = [];
}

public class CourtAvailabilityDto
{
    public string AvailabilityId { get; set; }
    
    public string PadelClubName { get; set; }
    
    public string CourtName { get; set; }
    
    public DateTime AvailabilityStartTimeAtLocalTimeZone { get; set; }
    
    public decimal Price { get; set; }
    
    public string BookingUrl { get; set; }
    
    public string BookingPlatform { get; set; }
    
    public int DurationInMinutes { get; set; }
    
    public CourtType CourtType { get; set; } = CourtType.Indoor;
}