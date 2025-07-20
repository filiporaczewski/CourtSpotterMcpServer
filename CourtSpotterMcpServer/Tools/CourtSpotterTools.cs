using System.ComponentModel;
using System.Text.Json;
using CourtSpotterMcpServer.Models;
using ModelContextProtocol.Server;

namespace CourtSpotterMcpServer.Tools;

[McpServerToolType]
public class CourtSpotterTools
{
    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CourtSpotterTools> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private const int DaysAhead = 14;

    public CourtSpotterTools(IHttpClientFactory httpClientFactory, TimeProvider timeProvider, ILogger<CourtSpotterTools> logger)
    {
        _httpClient = httpClientFactory.CreateClient("CourtSpotterClient");
        _timeProvider = timeProvider;
        _logger = logger;
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }
    
    [McpServerTool, Description("Get court availabilities for a specific date range. Returns a list of available court slots with details including club name, court type, price, duration, and booking URL. Available court types are indoor (0) and outdoor (1). The start time of each availability is converted to local (polish) time zone. Useful for finding available padel courts.")]
    public async Task<string> GetCourtAvailabilities([Description("Start date in YYYY-MM-DD format")] string startDate, [Description("End date in YYYY-MM-DD format")] string endDate)
    {
        if (!DateTime.TryParse(startDate, out var parsedStartDate) || !DateTime.TryParse(endDate, out var parsedEndDate))
        {
            _logger.LogWarning("Invalid date format provided. Start date: {StartDate}, End date: {EndDate}", startDate, endDate);
            
            return JsonSerializer.Serialize(new AvailabilitiesSearchResult
            {
                CourtAvailabilities = [],
                ErrorMessage = "Invalid date format. Please use YYYY-MM-DD.",
                Success = false
            }, _jsonOptions);
        }
        
        var startOfDay = parsedStartDate.Date;
        var start = TimeZoneInfo.ConvertTimeToUtc(startOfDay, _timeProvider.LocalTimeZone);
        
        var endOfDay = parsedEndDate.Date.AddDays(1).AddTicks(-1);
        var end = TimeZoneInfo.ConvertTimeToUtc(endOfDay, _timeProvider.LocalTimeZone);

        var today = _timeProvider.GetUtcNow().Date;
        if ((end - today).Days > DaysAhead)
        {
            _logger.LogWarning("Requested date range exceeds the maximum allowed range of {DaysAhead} days. Start date: {StartDate}, End date: {EndDate}", DaysAhead, startDate, endDate);
            
            return JsonSerializer.Serialize(new AvailabilitiesSearchResult
            {
                CourtAvailabilities = [],
                ErrorMessage = $"Requested date range exceeds the maximum allowed range of {endOfDay} days.",
                Success = false
            }, _jsonOptions);
        }

        try
        {
            var availabilitiesSearchResponse = await _httpClient.GetAsync($"api/court-availabilities?startDate={start:o}&endDate={end:o}");
            availabilitiesSearchResponse.EnsureSuccessStatusCode();
            var availabilitiesResponse = await availabilitiesSearchResponse.Content.ReadFromJsonAsync<CourtAvailabilitiesResponse>();
        
            if (availabilitiesResponse == null)
            {
                _logger.LogWarning("Failed to parse response from court-availabilities endpoint");
                return JsonSerializer.Serialize(new AvailabilitiesSearchResult
                {
                    CourtAvailabilities = [],
                    ErrorMessage = $"Failed to parse response from court-availabilities endpoint",
                    Success = false
                }, _jsonOptions);
            }
            
            var availabilities = availabilitiesResponse.CourtAvailabilities.Select(a => new CourtAvailabilityDto
            {
                AvailabilityStartTimeAtLocalTimeZone = TimeZoneInfo.ConvertTimeFromUtc(a.AvailabilityStartTime, _timeProvider.LocalTimeZone),
                DurationInMinutes = a.DurationInMinutes,
                AvailabilityId = a.AvailabilityId,
                BookingUrl = a.BookingUrl,
                PadelClubName = a.PadelClubName,
                CourtName = a.CourtName,
                CourtType = a.CourtType,
                Price = a.Price,
                PadelClubId = a.PadelClubId,
                BookingPlatform = a.BookingPlatform
            }).ToList();
            
            var result = new AvailabilitiesSearchResult
            {
                Success = true,
                CourtAvailabilities = availabilities,
                LocalTimeZone = _timeProvider.LocalTimeZone.Id
            };
            
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error fetching court availabilities for date range {StartDate} to {EndDate}", startDate, endDate);
            
            return JsonSerializer.Serialize(new AvailabilitiesSearchResult
            {
                Success = false,
                ErrorMessage = "Network error: Unable to connect to the court availability service. Please check your internet connection and try again.",
                CourtAvailabilities = []
            }, _jsonOptions);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout fetching court availabilities for date range {StartDate} to {EndDate}", 
                startDate, endDate);
            return JsonSerializer.Serialize(new AvailabilitiesSearchResult
            {
                Success = false,
                ErrorMessage = "Request timed out. The server took too long to respond. Please try again with a smaller date range.",
                CourtAvailabilities = []
            }, _jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error for court availabilities response");
            return JsonSerializer.Serialize(new AvailabilitiesSearchResult
            {
                Success = false,
                ErrorMessage = "Failed to parse the server response. The data format may be incorrect.",
                CourtAvailabilities = []
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching court availabilities for date range {StartDate} to {EndDate}", 
                startDate, endDate);
            return JsonSerializer.Serialize(new AvailabilitiesSearchResult
            {
                Success = false,
                ErrorMessage = "An unexpected error occurred while fetching court availabilities. Please try again later.",
                CourtAvailabilities = []
            }, _jsonOptions);
        }
    }
}