namespace EventApi.Application.DTOs;

public class CreateFeedbackDto
{
    public int EventId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
}

public class FeedbackDto
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public string EventTitle { get; set; } = null!;
    public int StudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime SubmittedAt { get; set; }
}
