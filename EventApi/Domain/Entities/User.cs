using EventApi.Domain.Enums;

namespace EventApi.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string PasswordSalt { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? StudentCode { get; set; }
    public string? Phone { get; set; }
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Event> Events { get; set; } = [];
    public ICollection<Registration> Registrations { get; set; } = [];
    public ICollection<Feedback> Feedbacks { get; set; } = [];
}
