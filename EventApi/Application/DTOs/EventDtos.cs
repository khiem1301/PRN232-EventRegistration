namespace EventApi.Application.DTOs;

public class EventListItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Status { get; set; } = null!;
    public string LocationName { get; set; } = null!;
    public string OrganizerName { get; set; } = null!;
    public int Capacity { get; set; }
    public int AvailableSlots { get; set; }
    public int RegisteredCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class LocationSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Address { get; set; } = null!;
}

public class OrganizerSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string ContactEmail { get; set; } = null!;
}

public class EventDetailDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public DateTime RegistrationDeadline { get; set; }
    public int Capacity { get; set; }
    public string Status { get; set; } = null!;
    public int RegisteredCount { get; set; }
    public int AvailableSlots { get; set; }
    public LocationSummaryDto Location { get; set; } = null!;
    public OrganizerSummaryDto Organizer { get; set; } = null!;
    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateEventDto
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public DateTime RegistrationDeadline { get; set; }
    public int Capacity { get; set; }
    public int LocationId { get; set; }
    public int OrganizerId { get; set; }
}

public class UpdateEventDto
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public DateTime RegistrationDeadline { get; set; }
    public int Capacity { get; set; }
    public int LocationId { get; set; }
    public int OrganizerId { get; set; }
}

public class EventQueryParams
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
