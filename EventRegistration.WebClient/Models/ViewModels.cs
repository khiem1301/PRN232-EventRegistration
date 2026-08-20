namespace EventRegistration.WebClient.Models;

public class UserViewModel
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? StudentCode { get; set; }
    public string? Phone { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class RegisterViewModel
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? StudentCode { get; set; }
    public string? Phone { get; set; }
}

public class LoginViewModel
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponseViewModel
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserViewModel User { get; set; } = new();
}

public class UpdateProfileViewModel
{
    public string FullName { get; set; } = string.Empty;
    public string? StudentCode { get; set; }
    public string? Phone { get; set; }
}

public class ChangePasswordViewModel
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class CreateUserViewModel
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? StudentCode { get; set; }
    public string? Phone { get; set; }
    public string Role { get; set; } = "Student";
}

public class EditUserViewModel
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? StudentCode { get; set; }
    public string? Phone { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class PagedUsersViewModel
{
    public List<UserViewModel> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public string? Search { get; set; }
}

public class LocationViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? Capacity { get; set; }
    public bool IsActive { get; set; }
}

public class OrganizerViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class EventListItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string OrganizerName { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int AvailableSlots { get; set; }
    public int RegisteredCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class EventDetailViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public DateTime RegistrationDeadline { get; set; }
    public int Capacity { get; set; }
    public string Status { get; set; } = string.Empty;
    public int RegisteredCount { get; set; }
    public int AvailableSlots { get; set; }
    public LocationSummaryViewModel Location { get; set; } = new();
    public OrganizerSummaryViewModel Organizer { get; set; } = new();
    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class LocationSummaryViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}

public class OrganizerSummaryViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
}

public class CreateEventViewModel
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartTime { get; set; } = DateTime.Now.AddDays(7);
    public DateTime EndTime { get; set; } = DateTime.Now.AddDays(7).AddHours(2);
    public DateTime RegistrationDeadline { get; set; } = DateTime.Now.AddDays(5);
    public int Capacity { get; set; } = 50;
    public int LocationId { get; set; }
    public int OrganizerId { get; set; }
    public List<LocationViewModel> Locations { get; set; } = [];
    public List<OrganizerViewModel> Organizers { get; set; } = [];
}

public class EditEventViewModel : CreateEventViewModel
{
    public int Id { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class PagedEventsViewModel
{
    public List<EventListItemViewModel> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class EventReportViewModel
{
    public int EventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int RegisteredCount { get; set; }
    public int CheckedInCount { get; set; }
    public double AttendanceRate { get; set; }
    public double FillRate { get; set; }
    public double? AverageRating { get; set; }
    public int FeedbackCount { get; set; }
}

public class OverviewReportViewModel
{
    public int TotalEvents { get; set; }
    public Dictionary<string, int> EventsByStatus { get; set; } = new();
    public int TotalRegistrations { get; set; }
    public int TotalCheckedIn { get; set; }
    public double OverallAttendanceRate { get; set; }
    public double OverallFillRate { get; set; }
    public double? OverallAverageRating { get; set; }
    public List<EventReportViewModel> EventReports { get; set; } = [];
}

public class PagedReportsViewModel
{
    public List<EventReportViewModel> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public string? Status { get; set; }
}

public class ApiErrorViewModel
{
    public string? Error { get; set; }
}
