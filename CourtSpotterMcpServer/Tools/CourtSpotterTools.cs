using System.ComponentModel;
using System.Text.Json;
using System.Text;
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
    
    private const string GetCourtAvailabilitiesDescription = """
                                                             Get court availabilities for a specific date range with optional filtering. Returns a list of available court slots with details including 
                                                             club name, court type, price, duration, and booking URL. Available court types are indoor (0) and outdoor (1). Available durations are 60, 90, 
                                                             and 120 minutes. The start time of each availability is converted to the club's local timezone. You can filter by specific club names.
                                                             """;
    
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
    
    [McpServerTool, Description(GetCourtAvailabilitiesDescription)]
    public async Task<string> GetCourtAvailabilities(
        [Description("Start date in YYYY-MM-DD format")] string startDate, 
        [Description("End date in YYYY-MM-DD format")] string endDate,
        [Description("Optional: Filter by court duration in minutes. Valid values: 60, 90, 120")] int[]? durations = null,
        [Description("Optional: Filter by specific club names (e.g., 'Padlovnia', 'Warsaw Padel Club')")] string[]? clubNames = null,
        [Description("Optional: Filter by court type. 0 for Indoor, 1 for Outdoor")] int? courtType = null)
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
            var clubsResponseMessage = await _httpClient.GetAsync("api/padel-clubs");
            clubsResponseMessage.EnsureSuccessStatusCode();
            var clubsResponse = await clubsResponseMessage.Content.ReadFromJsonAsync<PadelClubsResponse>();
            
            if (clubsResponse == null)
            {
                _logger.LogWarning("Failed to parse response from padel-clubs endpoint");
                return JsonSerializer.Serialize(new AvailabilitiesSearchResult
                {
                    CourtAvailabilities = [],
                    ErrorMessage = "Failed to retrieve club information for timezone conversion",
                    Success = false
                }, _jsonOptions);
            }
            
            var clubTimezones = clubsResponse.Clubs.ToDictionary(c => c.Name, c => c.TimeZone);
            
            // Create club name to ID mapping for filtering
            var clubNameToId = clubsResponse.Clubs.ToDictionary(
                c => c.Name,
                c => c.ClubId,
                StringComparer.OrdinalIgnoreCase);
            
            var queryBuilder = new StringBuilder($"api/court-availabilities?startDate={start:o}&endDate={end:o}");
            
            if (durations != null && durations.Length > 0)
            {
                var validDurations = durations.Where(d => d == 60 || d == 90 || d == 120).ToArray();
                if (validDurations.Length > 0)
                {
                    foreach (var duration in validDurations)
                    {
                        queryBuilder.Append($"&durations={duration}");
                    }
                }
            }
            
            if (clubNames != null && clubNames.Length > 0)
            {
                var matchedClubIds = clubNames
                    .Where(name => clubNameToId.ContainsKey(name))
                    .Select(name => clubNameToId[name])
                    .ToArray();
                
                if (matchedClubIds.Length > 0)
                {
                    foreach (var clubId in matchedClubIds)
                    {
                        queryBuilder.Append($"&clubIds={Uri.EscapeDataString(clubId)}");
                    }
                }
                
                // Log if some club names were not found
                var unmatchedNames = clubNames.Where(name => !clubNameToId.ContainsKey(name)).ToArray();
                if (unmatchedNames.Length > 0)
                {
                    _logger.LogWarning("Club names not found: {UnmatchedNames}. Available clubs: {AvailableClubs}", 
                        string.Join(", ", unmatchedNames), 
                        string.Join(", ", clubNameToId.Keys));
                }
            }
            
            if (courtType.HasValue && (courtType.Value == 0 || courtType.Value == 1))
            {
                queryBuilder.Append($"&courtType={courtType.Value}");
            }
            
            _logger.LogInformation("Fetching court availabilities with query: {Query}", queryBuilder.ToString());
            
            var availabilitiesSearchResponse = await _httpClient.GetAsync(queryBuilder.ToString());
            availabilitiesSearchResponse.EnsureSuccessStatusCode();
            var availabilitiesResponse = await availabilitiesSearchResponse.Content.ReadFromJsonAsync<CourtAvailabilitiesResponse>();
        
            if (availabilitiesResponse == null)
            {
                _logger.LogWarning("Failed to parse response from court-availabilities endpoint");
                return JsonSerializer.Serialize(new AvailabilitiesSearchResult
                {
                    CourtAvailabilities = [],
                    ErrorMessage = "Failed to parse response from court-availabilities endpoint",
                    Success = false
                }, _jsonOptions);
            }
            
            var availabilities = availabilitiesResponse.CourtAvailabilities.Select(a => 
            {
                // Use club-specific timezone if available, fallback to server timezone
                TimeZoneInfo clubTimeZone;
                try
                {
                    var timeZoneId = clubTimezones.GetValueOrDefault(a.PadelClubName);
                    clubTimeZone = !string.IsNullOrEmpty(timeZoneId) 
                        ? TimeZoneInfo.FindSystemTimeZoneById(timeZoneId)
                        : _timeProvider.LocalTimeZone;
                }
                catch (TimeZoneNotFoundException)
                {
                    _logger.LogWarning("Invalid timezone {TimeZone} for club {ClubName}, using server timezone", 
                        clubTimezones.GetValueOrDefault(a.PadelClubName), a.PadelClubName);
                    clubTimeZone = _timeProvider.LocalTimeZone;
                }
                
                return new CourtAvailabilityDto
                {
                    AvailabilityStartTimeAtLocalTimeZone = TimeZoneInfo.ConvertTimeFromUtc(a.AvailabilityStartTime, clubTimeZone),
                    DurationInMinutes = a.DurationInMinutes,
                    AvailabilityId = a.AvailabilityId,
                    BookingUrl = a.BookingUrl,
                    PadelClubName = a.PadelClubName,
                    CourtName = a.CourtName,
                    CourtType = a.CourtType,
                    Price = a.Price,
                    BookingPlatform = a.BookingPlatform
                };
            }).ToList();
            
            var result = new AvailabilitiesSearchResult
            {
                Success = true,
                CourtAvailabilities = availabilities
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