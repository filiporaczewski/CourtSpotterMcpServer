namespace CourtSpotterMcpServer.Models;

public class AvailabilitiesSearchResult
{
    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    public string LocalTimeZone { get; set; }
    
    public List<CourtAvailability> CourtAvailabilities { get; set; } = [];
}