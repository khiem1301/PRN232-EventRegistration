namespace EventApi.Domain.Entities;

public class Event
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public DateTime RegistrationDeadline { get; set; }
    public int Capacity { get; set; }
    public string Status { get; set; } = "Draft";
    public int LocationId { get; set; }
    public int OrganizerId { get; set; }
    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }

    public Location Location { get; set; } = null!;
    public Organizer Organizer { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    public ICollection<Registration> Registrations { get; set; } = [];
    public ICollection<Feedback> Feedbacks { get; set; } = [];
}
