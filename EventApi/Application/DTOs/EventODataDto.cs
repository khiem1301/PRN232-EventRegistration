namespace EventApi.Application.DTOs;

public class EventODataDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Status { get; set; } = null!;
    public int Capacity { get; set; }
    public int RegisteredCount { get; set; }
    public int AvailableSlots { get; set; }
    public string LocationName { get; set; } = null!;
    public string OrganizerName { get; set; } = null!;
}
