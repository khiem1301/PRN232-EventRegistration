namespace EventApi.Domain.Entities;

public class Registration
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public int StudentId { get; set; }
    public string Status { get; set; } = "Registered";
    public DateTime RegisteredAt { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    public Event Event { get; set; } = null!;
    public User Student { get; set; } = null!;
}
