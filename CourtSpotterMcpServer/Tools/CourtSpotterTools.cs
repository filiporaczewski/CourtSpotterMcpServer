using System.ComponentModel;
using CourtSpotterMcpServer.Models;
using ModelContextProtocol.Server;

namespace CourtSpotterMcpServer.Tools;

[McpServerToolType]
public class CourtSpotterTools
{
    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CourtSpotterTools> _logger;

    public CourtSpotterTools(IHttpClientFactory httpClientFactory, TimeProvider timeProvider, ILogger<CourtSpotterTools> logger)
    {
        _httpClient = httpClientFactory.CreateClient("CourtSpotterClient");
        _timeProvider = timeProvider;
        _logger = logger;
    }
    
    [McpServerTool, Description("Get court availabilities for a specific date range")]
    public async Task<List<CourtAvailability>> GetCourtAvailabilities([Description("Start date in YYYY-MM-DD format")] string startDate, [Description("End date in YYYY-MM-DD format")] string endDate)
    {
        if (!DateTime.TryParse(startDate, out var parsedStartDate) || !DateTime.TryParse(endDate, out var parsedEndDate))
        {
            _logger.LogWarning("Invalid date format provided. Start date: {StartDate}, End date: {EndDate}", startDate, endDate);
            return [];
        }
        
        var startOfDay = parsedStartDate.Date;
        var start = TimeZoneInfo.ConvertTimeToUtc(startOfDay, _timeProvider.LocalTimeZone);
        
        var endOfDay = parsedEndDate.Date.AddDays(1).AddTicks(-1);
        var end = TimeZoneInfo.ConvertTimeToUtc(endOfDay, _timeProvider.LocalTimeZone);

        try
        {
            var response = await _httpClient.GetAsync($"api/court-availabilities?startDate={start:o}&endDate={end:o}");
            response.EnsureSuccessStatusCode();
            
            var availabilitiesResponse = await response.Content.ReadFromJsonAsync<CourtAvailabilitiesResponse>();
        
            if (availabilitiesResponse == null)
            {
                _logger.LogWarning("Failed to parse response from court-availabilities endpoint");
                return [];
            }
            
            return availabilitiesResponse.CourtAvailabilities.Select(a => new CourtAvailability
            {
                DateTime = TimeZoneInfo.ConvertTimeFromUtc(a.DateTime, _timeProvider.LocalTimeZone),
                DurationInMinutes = a.DurationInMinutes,
                Id = a.Id,
                BookingUrl = a.BookingUrl,
                ClubName = a.ClubName,
                CourtName = a.CourtName,
                CourtType = a.CourtType,
                Price = a.Price,
                ClubId = a.ClubId,
                Provider = a.Provider
            }).ToList();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error fetching court availabilities for date range {StartDate} to {EndDate}", startDate, endDate);
            return [];
        }
    }
}