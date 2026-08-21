namespace EventApi.Application.DTOs;

public class RegisterEventDto
{
    public int EventId { get; set; }
}

public class CheckInRegistrationDto
{
    public string Status { get; set; } = "Attended";
}

public class RegistrationDto
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public string EventTitle { get; set; } = null!;
    public int StudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime RegisteredAt { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}
