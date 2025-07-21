namespace CourtSpotterMcpServer.Models;

public class PadelClubsResponse
{
    public int TotalCount { get; set; }
    public List<PadelClubDto> Clubs { get; set; } = [];
}

public class PadelClubDto
{
    public string ClubId { get; set; }
    public string Name { get; set; }
    public string Provider { get; set; }
    public int? PagesCount { get; set; }
    public string TimeZone { get; set; }
}