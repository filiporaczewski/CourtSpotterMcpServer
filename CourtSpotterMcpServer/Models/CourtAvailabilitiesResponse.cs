using System.Text.Json.Serialization;

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
    [JsonPropertyName("id")]
    public string AvailabilityId { get; set; }

    [JsonPropertyName("clubId")]
    public string PadelClubId { get; set; }

    [JsonPropertyName("clubName")]
    public string PadelClubName { get; set; }
    
    [JsonPropertyName("courtName")]
    public string CourtName { get; set; }
    
    [JsonPropertyName("dateTime")]
    public DateTime AvailabilityStartTime { get; set; }
   
    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("bookingUrl")]
    public string BookingUrl { get; set; }
    
    [JsonPropertyName("provider")]
    public string BookingPlatform { get; set; }
    
    [JsonPropertyName("durationInMinutes")]
    public int DurationInMinutes { get; set; }
    
    [JsonPropertyName("courtType")]
    public CourtType CourtType { get; set; } = CourtType.Indoor;
}