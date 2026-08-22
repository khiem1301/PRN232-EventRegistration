using EventApi.Application.DTOs;
using EventApi.Domain.Common;

namespace EventApi.Application.Interfaces;

public interface IAuthService
{
    Task<Result<UserDto>> RegisterAsync(RegisterRequestDto request);
    Task<Result<LoginResponseDto>> LoginAsync(LoginRequestDto request);
}

public interface IAccountService
{
    Task<Result<UserDto>> GetMeAsync(int userId);
    Task<Result<UserDto>> UpdateMeAsync(int userId, UpdateProfileDto request);
    Task<Result<object>> ChangePasswordAsync(int userId, ChangePasswordDto request);
}

public interface IUserService
{
    Task<Result<PagedResult<UserDto>>> GetAllAsync(string? search, int page, int pageSize);
    Task<Result<UserDto>> GetByIdAsync(int id);
    Task<Result<UserDto>> CreateAsync(CreateUserDto request);
    Task<Result<UserDto>> UpdateAsync(int id, UpdateUserDto request);
    Task<Result<object>> DeactivateAsync(int id);
}

public interface ILocationService
{
    Task<Result<IEnumerable<LocationDto>>> GetAllAsync();
    Task<Result<LocationDto>> GetByIdAsync(int id);
    Task<Result<LocationDto>> CreateAsync(CreateLocationDto request);
    Task<Result<LocationDto>> UpdateAsync(int id, UpdateLocationDto request);
    Task<Result<object>> DeleteAsync(int id);
}

public interface IOrganizerService
{
    Task<Result<IEnumerable<OrganizerDto>>> GetAllAsync();
    Task<Result<OrganizerDto>> GetByIdAsync(int id);
    Task<Result<OrganizerDto>> CreateAsync(CreateOrganizerDto request);
    Task<Result<OrganizerDto>> UpdateAsync(int id, UpdateOrganizerDto request);
    Task<Result<object>> DeleteAsync(int id);
}

public interface IEventService
{
    Task<Result<PagedResult<EventListItemDto>>> GetAllAsync(EventQueryParams query, string? userRole);
    Task<Result<EventDetailDto>> GetByIdAsync(int id, string? userRole);
    Task<Result<EventDetailDto>> CreateAsync(CreateEventDto request, int userId);
    Task<Result<EventDetailDto>> UpdateAsync(int id, UpdateEventDto request, string userRole);
    Task<Result<EventDetailDto>> ChangeStatusAsync(int id, string targetStatus, string userRole);
}

public interface IReportService
{
    Task<Result<EventReportDto>> GetEventReportAsync(int eventId);
    Task<Result<PagedResult<EventReportDto>>> GetEventsReportAsync(string? status, int page, int pageSize);
    Task<Result<OverviewReportDto>> GetOverviewAsync();
}

public interface IRegistrationService
{
    Task<Result<IEnumerable<RegistrationDto>>> GetMyRegistrationsAsync(int studentId);
    Task<Result<IEnumerable<RegistrationDto>>> GetEventRegistrationsAsync(int eventId, string? userRole);
    Task<Result<IEnumerable<RegistrationDto>>> MarkNoShowAsync(int eventId, string userRole);
    Task<Result<RegistrationDto>> RegisterAsync(int studentId, RegisterEventDto request);
    Task<Result<RegistrationDto>> CancelAsync(int registrationId, int userId, string? userRole);
    Task<Result<RegistrationDto>> CheckInAsync(int registrationId, string userRole, CheckInRegistrationDto request);
}

public interface IFeedbackService
{
    Task<Result<IEnumerable<FeedbackDto>>> GetEventFeedbacksAsync(int eventId);
    Task<Result<IEnumerable<FeedbackDto>>> GetMyFeedbacksAsync(int studentId);
    Task<Result<FeedbackDto>> SubmitAsync(int studentId, CreateFeedbackDto request);
}
