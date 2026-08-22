using EventApi.Application.DTOs;
using EventApi.Application.Interfaces;
using EventApi.Domain.Common;
using EventApi.Domain.Entities;
using EventApi.Domain.Enums;
using EventApi.Infrastructure.Data;
using EventApi.Protos;
using Grpc.Net.Client;
using Microsoft.EntityFrameworkCore;

namespace EventApi.Application.Services;

public class RegistrationService : IRegistrationService
{
    private readonly AppDbContext _context;
    private readonly ILogger<RegistrationService> _logger;
    private readonly IConfiguration _configuration;

    public RegistrationService(
        AppDbContext context,
        ILogger<RegistrationService> logger,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<Result<IEnumerable<RegistrationDto>>> GetMyRegistrationsAsync(int studentId)
    {
        var items = await _context.Registrations
            .AsNoTracking()
            .Include(r => r.Event)
            .Include(r => r.Student)
            .Where(r => r.StudentId == studentId)
            .OrderByDescending(r => r.RegisteredAt)
            .ToListAsync();

        return Result<IEnumerable<RegistrationDto>>.Success(items.Select(MapToDto));
    }

    public async Task<Result<IEnumerable<RegistrationDto>>> GetEventRegistrationsAsync(int eventId, string? userRole)
    {
        if (userRole is not ("Staff" or "Admin"))
            return Result<IEnumerable<RegistrationDto>>.Fail("Unauthorized.", 403);

        var items = await _context.Registrations
            .AsNoTracking()
            .Include(r => r.Event)
            .Include(r => r.Student)
            .Where(r => r.EventId == eventId)
            .OrderByDescending(r => r.RegisteredAt)
            .ToListAsync();

        return Result<IEnumerable<RegistrationDto>>.Success(items.Select(MapToDto));
    }

    public async Task<Result<IEnumerable<RegistrationDto>>> MarkNoShowAsync(int eventId, string userRole)
    {
        if (userRole is not ("Staff" or "Admin"))
            return Result<IEnumerable<RegistrationDto>>.Fail("Unauthorized.", 403);

        var registrations = await _context.Registrations
            .Include(r => r.Event)
            .Include(r => r.Student)
            .Where(r => r.EventId == eventId)
            .ToListAsync();

        if (registrations.Count == 0)
            return Result<IEnumerable<RegistrationDto>>.Success([]);

        if (registrations[0].Event.EndTime > DateTime.UtcNow)
            return Result<IEnumerable<RegistrationDto>>.Fail("No-show can be marked only after the event has ended.", 400);

        foreach (var registration in registrations.Where(r => r.Status == "Registered"))
        {
            registration.Status = "NoShow";
            registration.CheckedInAt = null;
        }

        await _context.SaveChangesAsync();
        return Result<IEnumerable<RegistrationDto>>.Success(registrations.Select(MapToDto));
    }

    public async Task<Result<RegistrationDto>> RegisterAsync(int studentId, RegisterEventDto request)
    {
        var evt = await _context.Events
            .Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == request.EventId);

        if (evt is null)
            return Result<RegistrationDto>.Fail("Event not found.", 404);

        await SyncEventStatusAsync(evt);

        if (evt.Status == nameof(EventStatus.Cancelled))
            return Result<RegistrationDto>.Fail("This event has been cancelled.", 400);

        if (evt.Status != nameof(EventStatus.Published))
            return Result<RegistrationDto>.Fail("Event is not open for registration.", 400);

        var now = DateTime.UtcNow;
        if (evt.StartTime <= now)
            return Result<RegistrationDto>.Fail("Registration is closed because the event has already started.", 400);

        if (evt.RegistrationDeadline < now)
            return Result<RegistrationDto>.Fail("Registration deadline has passed.", 400);

        var activeCount = evt.Registrations.Count(r => r.Status != "Cancelled");
        if (activeCount >= evt.Capacity)
            return Result<RegistrationDto>.Fail("No available slots left for this event.", 400);

        var existing = await _context.Registrations
            .FirstOrDefaultAsync(r => r.EventId == request.EventId && r.StudentId == studentId);

        if (existing is not null)
        {
            if (existing.Status != "Cancelled")
                return Result<RegistrationDto>.Fail("You already registered for this event.", 400);

            existing.Status = "Registered";
            existing.RegisteredAt = DateTime.UtcNow;
            existing.CheckedInAt = null;
            existing.CancelledAt = null;
            await _context.SaveChangesAsync();
            await SendRegistrationNotificationAsync(existing.EventId, evt.Title, studentId);
            return Result<RegistrationDto>.Success(MapToDto(existing));
        }

        var registration = new Registration
        {
            EventId = evt.Id,
            StudentId = studentId,
            Status = "Registered",
            RegisteredAt = DateTime.UtcNow,
            CheckedInAt = null,
            CancelledAt = null
        };

        _context.Registrations.Add(registration);
        await _context.SaveChangesAsync();

        await SendRegistrationNotificationAsync(evt.Id, evt.Title, studentId);

        return Result<RegistrationDto>.Success(MapToDto(registration));
    }

    public async Task<Result<RegistrationDto>> CancelAsync(int registrationId, int userId, string? userRole)
    {
        var registration = await _context.Registrations
            .Include(r => r.Event)
            .Include(r => r.Student)
            .FirstOrDefaultAsync(r => r.Id == registrationId);

        if (registration is null)
            return Result<RegistrationDto>.Fail("Registration not found.", 404);

        if (registration.StudentId != userId && userRole is not ("Staff" or "Admin"))
            return Result<RegistrationDto>.Fail("Unauthorized.", 403);

        if (registration.Status == "Cancelled")
            return Result<RegistrationDto>.Fail("This registration is already cancelled.", 400);

        if (registration.Status != "Registered")
            return Result<RegistrationDto>.Fail("Only a registered ticket can be cancelled.", 400);

        if (registration.Event.StartTime <= DateTime.UtcNow)
            return Result<RegistrationDto>.Fail("Registration cannot be cancelled after the event has started.", 400);

        registration.Status = "Cancelled";
        registration.CancelledAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Result<RegistrationDto>.Success(MapToDto(registration));
    }

    public async Task<Result<RegistrationDto>> CheckInAsync(int registrationId, string userRole, CheckInRegistrationDto request)
    {
        if (userRole is not ("Staff" or "Admin"))
            return Result<RegistrationDto>.Fail("Unauthorized.", 403);

        var validStatuses = new[] { "Attended", "NoShow" };
        if (!validStatuses.Contains(request.Status))
            return Result<RegistrationDto>.Fail("Status must be either Attended or NoShow.", 400);

        var registration = await _context.Registrations
            .Include(r => r.Event)
            .Include(r => r.Student)
            .FirstOrDefaultAsync(r => r.Id == registrationId);

        if (registration is null)
            return Result<RegistrationDto>.Fail("Registration not found.", 404);

        await SyncEventStatusAsync(registration.Event);

        if (registration.Status == "Cancelled")
            return Result<RegistrationDto>.Fail("Cannot check in a cancelled registration.", 400);

        if (registration.Status != "Registered")
            return Result<RegistrationDto>.Fail("Only a registered ticket can be checked in.", 400);

        if (request.Status == "Attended")
        {
            if (registration.Event.Status is not (nameof(EventStatus.Published) or nameof(EventStatus.Ongoing)))
                return Result<RegistrationDto>.Fail("Check-in is available only for published or ongoing events.", 400);

            registration.Status = "Attended";
            registration.CheckedInAt = DateTime.UtcNow;
        }
        else if (request.Status == "NoShow")
        {
            if (registration.Event.EndTime > DateTime.UtcNow)
                return Result<RegistrationDto>.Fail("No-show can be marked only after the event has ended.", 400);

            registration.Status = "NoShow";
            registration.CheckedInAt = null;
        }

        await _context.SaveChangesAsync();
        return Result<RegistrationDto>.Success(MapToDto(registration));
    }

    private async Task SyncEventStatusAsync(Event evt)
    {
        var before = evt.Status;
        EventWorkflow.ApplyResolvedStatus(evt);
        if (before != evt.Status)
            await _context.SaveChangesAsync();
    }

    private async Task SendRegistrationNotificationAsync(int eventId, string eventTitle, int studentId)
    {
        try
        {
            var user = await _context.Users.FindAsync(studentId);
            var notification = new RegistrationNotificationRequest
            {
                EventId = eventId,
                EventTitle = eventTitle,
                UserName = user?.FullName ?? "Student",
                UserEmail = user?.Email ?? string.Empty
            };

            var grpcUrl = _configuration["NotificationGrpc:BaseUrl"] ?? "http://localhost:5090";
            using var grpcChannel = GrpcChannel.ForAddress(grpcUrl);
            var client = new NotificationService.NotificationServiceClient(grpcChannel);
            var reply = await client.SendRegistrationConfirmationAsync(notification);
            if (!reply.Success)
                _logger.LogWarning("gRPC notification failed: {Message}", reply.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "gRPC notification not available; registration still succeeded.");
        }
    }

    private static RegistrationDto MapToDto(Registration registration) => new()
    {
        Id = registration.Id,
        EventId = registration.EventId,
        EventTitle = registration.Event?.Title ?? string.Empty,
        StudentId = registration.StudentId,
        StudentName = registration.Student?.FullName ?? string.Empty,
        Status = registration.Status,
        RegisteredAt = registration.RegisteredAt,
        CheckedInAt = registration.CheckedInAt,
        CancelledAt = registration.CancelledAt
    };
}
