namespace EventApi.Application.DTOs;

public class EventReportDto
{
    public int EventId { get; set; }
    public string Title { get; set; } = null!;
    public string Status { get; set; } = null!;
    public int Capacity { get; set; }
    public int RegisteredCount { get; set; }
    public int CheckedInCount { get; set; }
    public double AttendanceRate { get; set; }
    public double FillRate { get; set; }
    public double? AverageRating { get; set; }
    public int FeedbackCount { get; set; }
}

public class EventsReportListDto
{
    public List<EventReportDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class OverviewReportDto
{
    public int TotalEvents { get; set; }
    public Dictionary<string, int> EventsByStatus { get; set; } = new();
    public int TotalRegistrations { get; set; }
    public int TotalCheckedIn { get; set; }
    public double OverallAttendanceRate { get; set; }
    public double OverallFillRate { get; set; }
    public double? OverallAverageRating { get; set; }
}
