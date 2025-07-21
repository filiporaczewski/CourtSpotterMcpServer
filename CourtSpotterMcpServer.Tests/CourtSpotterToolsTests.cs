using System.Net;
using System.Text;
using System.Text.Json;
using CourtSpotterMcpServer.Models;
using CourtSpotterMcpServer.Tools;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Shouldly;

namespace CourtSpotterMcpServer.Tests;

public class CourtSpotterToolsTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly CourtSpotterTools _courtSpotterTools;

    public CourtSpotterToolsTests()
    {
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        var timeProviderMock = new Mock<TimeProvider>();
        var loggerMock = new Mock<ILogger<CourtSpotterTools>>();
        var polishTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://api-courtspotter.azurewebsites.net/")
        };

        httpClientFactoryMock.Setup(x => x.CreateClient("CourtSpotterClient"))
            .Returns(httpClient);

        timeProviderMock.Setup(x => x.LocalTimeZone).Returns(polishTimeZone);
        timeProviderMock.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero));

        _courtSpotterTools = new CourtSpotterTools(httpClientFactoryMock.Object, timeProviderMock.Object, loggerMock.Object);
    }

    [Fact]
    public async Task GetCourtAvailabilities_WithValidDateRange_ReturnsAvailabilitiesWithClubTimezone()
    {
        var clubsResponse = new PadelClubsResponse
        {
            TotalCount = 1,
            Clubs = new List<PadelClubDto>
            {
                new() { ClubId = "club1", Name = "Test Club", Provider = "TestProvider", TimeZone = "Europe/Warsaw" }
            }
        };

        var availabilitiesResponse = new CourtAvailabilitiesResponse
        {
            CourtAvailabilities = new List<CourtAvailability>
            {
                new()
                {
                    AvailabilityId = "1",
                    AvailabilityStartTime = new DateTime(2024, 1, 15, 14, 0, 0, DateTimeKind.Utc),
                    DurationInMinutes = 90,
                    PadelClubName = "Test Club",
                    CourtName = "Court 1",
                    CourtType = CourtType.Indoor,
                    Price = 100.0m,
                    BookingUrl = "https://test.com/book",
                    BookingPlatform = "TestPlatform"
                }
            }
        };

        var clubsJson = JsonSerializer.Serialize(clubsResponse, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var availabilitiesJson = JsonSerializer.Serialize(availabilitiesResponse, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("api/padel-clubs")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(clubsJson, Encoding.UTF8, "application/json")
            });

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("api/court-availabilities")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(availabilitiesJson, Encoding.UTF8, "application/json")
            });

        var result = await _courtSpotterTools.GetCourtAvailabilities(startDate: "2024-01-15", endDate: "2024-01-16");

        result.ShouldNotBeNull();
        var resultObj = JsonSerializer.Deserialize<AvailabilitiesSearchResult>(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        
        resultObj!.Success.ShouldBeTrue();
        resultObj.CourtAvailabilities.ShouldNotBeNull();
        resultObj.CourtAvailabilities.Count.ShouldBe(1);
        resultObj.CourtAvailabilities[0].PadelClubName.ShouldBe("Test Club");
        // Clubs are not returned in response, only used internally for timezone conversion
    }

    [Fact]
    public async Task GetCourtAvailabilities_WithFilterParameters_BuildsCorrectQuery()
    {
        var clubsResponse = new PadelClubsResponse
        {
            TotalCount = 1,
            Clubs = new List<PadelClubDto>
            {
                new() { ClubId = "club1", Name = "Test Club", Provider = "TestProvider", TimeZone = "Europe/Warsaw" }
            }
        };

        var availabilitiesResponse = new CourtAvailabilitiesResponse
        {
            CourtAvailabilities = new List<CourtAvailability>()
        };

        var clubsJson = JsonSerializer.Serialize(clubsResponse, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var availabilitiesJson = JsonSerializer.Serialize(availabilitiesResponse, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("api/padel-clubs")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(clubsJson, Encoding.UTF8, "application/json")
            });

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.RequestUri!.ToString().Contains("api/court-availabilities") &&
                    req.RequestUri.ToString().Contains("durations=90") &&
                    req.RequestUri.ToString().Contains("clubIds=club1") &&
                    req.RequestUri.ToString().Contains("courtType=0")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(availabilitiesJson, Encoding.UTF8, "application/json")
            });

        var result = await _courtSpotterTools.GetCourtAvailabilities(
            startDate: "2024-01-15", 
            endDate: "2024-01-16", 
            durations: new[] { 90 }, 
            clubNames: new[] { "Test Club" }, 
            courtType: 0);

        result.ShouldNotBeNull();
        var resultObj = JsonSerializer.Deserialize<AvailabilitiesSearchResult>(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        
        resultObj!.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task GetCourtAvailabilities_WithInvalidDateFormat_ReturnsError()
    {
        var result = await _courtSpotterTools.GetCourtAvailabilities(startDate: "invalid-date", endDate: "2024-01-16");

        result.ShouldNotBeNull();
        var resultObj = JsonSerializer.Deserialize<AvailabilitiesSearchResult>(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        
        resultObj!.Success.ShouldBeFalse();
        resultObj.ErrorMessage.ShouldContain("Invalid date format");
    }

    [Fact]
    public async Task GetCourtAvailabilities_WithDateRangeExceedingLimit_ReturnsError()
    {
        var result = await _courtSpotterTools.GetCourtAvailabilities(startDate: "2024-01-15", endDate: "2024-02-15");

        result.ShouldNotBeNull();
        var resultObj = JsonSerializer.Deserialize<AvailabilitiesSearchResult>(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        
        resultObj!.Success.ShouldBeFalse();
        resultObj.ErrorMessage.ShouldContain("maximum allowed range");
    }

    [Fact]
    public async Task GetCourtAvailabilities_WithNetworkError_ReturnsErrorMessage()
    {
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var result = await _courtSpotterTools.GetCourtAvailabilities(startDate: "2024-01-15", endDate: "2024-01-16");

        result.ShouldNotBeNull();
        var resultObj = JsonSerializer.Deserialize<AvailabilitiesSearchResult>(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        
        resultObj!.Success.ShouldBeFalse();
        resultObj.ErrorMessage.ShouldContain("Network error");
    }

    [Fact]
    public async Task GetCourtAvailabilities_UsesClubTimezoneForTimeConversion()
    {
        var clubsResponse = new PadelClubsResponse
        {
            TotalCount = 2,
            Clubs = new List<PadelClubDto>
            {
                new() { ClubId = "club1", Name = "Warsaw Club", Provider = "Provider1", TimeZone = "Europe/Warsaw" },
                new() { ClubId = "club2", Name = "London Club", Provider = "Provider2", TimeZone = "Europe/London" }
            }
        };

        var availabilitiesResponse = new CourtAvailabilitiesResponse
        {
            CourtAvailabilities = new List<CourtAvailability>
            {
                new()
                {
                    AvailabilityId = "1",
                    AvailabilityStartTime = new DateTime(2024, 1, 15, 14, 0, 0, DateTimeKind.Utc),
                    DurationInMinutes = 90,
                    PadelClubName = "Warsaw Club",
                    CourtName = "Court 1",
                    CourtType = CourtType.Indoor,
                    Price = 100.0m,
                    BookingUrl = "https://test.com/book",
                    BookingPlatform = "TestPlatform"
                },
                new()
                {
                    AvailabilityId = "2",
                    AvailabilityStartTime = new DateTime(2024, 1, 15, 14, 0, 0, DateTimeKind.Utc),
                    DurationInMinutes = 90,
                    PadelClubName = "London Club",
                    CourtName = "Court 2",
                    CourtType = CourtType.Indoor,
                    Price = 120.0m,
                    BookingUrl = "https://test2.com/book",
                    BookingPlatform = "TestPlatform"
                }
            }
        };

        var clubsJson = JsonSerializer.Serialize(clubsResponse, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var availabilitiesJson = JsonSerializer.Serialize(availabilitiesResponse, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("api/padel-clubs")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(clubsJson, Encoding.UTF8, "application/json")
            });

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("api/court-availabilities")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(availabilitiesJson, Encoding.UTF8, "application/json")
            });

        var result = await _courtSpotterTools.GetCourtAvailabilities(startDate: "2024-01-15", endDate: "2024-01-16");

        result.ShouldNotBeNull();
        var resultObj = JsonSerializer.Deserialize<AvailabilitiesSearchResult>(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        
        resultObj!.Success.ShouldBeTrue();
        resultObj.CourtAvailabilities.Count.ShouldBe(2);
        
        // Each availability should use the timezone from its corresponding club
        var warsawAvailability = resultObj.CourtAvailabilities.First(a => a.PadelClubName == "Warsaw Club");
        var londonAvailability = resultObj.CourtAvailabilities.First(a => a.PadelClubName == "London Club");
        
        // The local times should be different due to different timezones
        warsawAvailability.AvailabilityStartTimeAtLocalTimeZone.Hour.ShouldBe(15); // UTC+1 in January
        londonAvailability.AvailabilityStartTimeAtLocalTimeZone.Hour.ShouldBe(14); // UTC+0 in January
    }

    [Fact]
    public async Task GetCourtAvailabilities_WithClubNameFiltering_MapsNamesToIds()
    {
        var clubsResponse = new PadelClubsResponse
        {
            TotalCount = 2,
            Clubs = new List<PadelClubDto>
            {
                new() { ClubId = "club1", Name = "Warsaw Club", Provider = "Provider1", TimeZone = "Europe/Warsaw" },
                new() { ClubId = "club2", Name = "London Club", Provider = "Provider2", TimeZone = "Europe/London" }
            }
        };

        var availabilitiesResponse = new CourtAvailabilitiesResponse
        {
            CourtAvailabilities = new List<CourtAvailability>()
        };

        var clubsJson = JsonSerializer.Serialize(clubsResponse, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var availabilitiesJson = JsonSerializer.Serialize(availabilitiesResponse, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("api/padel-clubs")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(clubsJson, Encoding.UTF8, "application/json")
            });

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.RequestUri!.ToString().Contains("api/court-availabilities") &&
                    req.RequestUri.ToString().Contains("clubIds=club1") &&
                    req.RequestUri.ToString().Contains("clubIds=club2")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(availabilitiesJson, Encoding.UTF8, "application/json")
            });

        var result = await _courtSpotterTools.GetCourtAvailabilities(
            startDate: "2024-01-15", 
            endDate: "2024-01-16", 
            clubNames: new[] { "Warsaw Club", "London Club" });

        result.ShouldNotBeNull();
        var resultObj = JsonSerializer.Deserialize<AvailabilitiesSearchResult>(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        
        resultObj!.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task GetCourtAvailabilities_WithNonExistentClubName_IgnoresUnmatchedNames()
    {
        var clubsResponse = new PadelClubsResponse
        {
            TotalCount = 1,
            Clubs = new List<PadelClubDto>
            {
                new() { ClubId = "club1", Name = "Warsaw Club", Provider = "Provider1", TimeZone = "Europe/Warsaw" }
            }
        };

        var availabilitiesResponse = new CourtAvailabilitiesResponse
        {
            CourtAvailabilities = new List<CourtAvailability>()
        };

        var clubsJson = JsonSerializer.Serialize(clubsResponse, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var availabilitiesJson = JsonSerializer.Serialize(availabilitiesResponse, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("api/padel-clubs")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(clubsJson, Encoding.UTF8, "application/json")
            });

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.RequestUri!.ToString().Contains("api/court-availabilities") &&
                    req.RequestUri.ToString().Contains("clubIds=club1") &&
                    !req.RequestUri.ToString().Contains("NonExistentClub")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(availabilitiesJson, Encoding.UTF8, "application/json")
            });

        var result = await _courtSpotterTools.GetCourtAvailabilities(
            startDate: "2024-01-15", 
            endDate: "2024-01-16", 
            clubNames: new[] { "Warsaw Club", "NonExistent Club" });

        result.ShouldNotBeNull();
        var resultObj = JsonSerializer.Deserialize<AvailabilitiesSearchResult>(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        
        resultObj!.Success.ShouldBeTrue();
    }
}