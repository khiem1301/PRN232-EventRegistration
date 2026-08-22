using AutoMapper;
using EventApi.Application.DTOs;
using EventApi.Application.Interfaces;
using EventApi.Domain.Common;
using EventApi.Domain.Entities;
using EventApi.Domain.Enums;
using EventApi.Infrastructure.Data;
using EventApi.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventApi.Application.Services;

public class EventService : IEventService
{
    private readonly IUnitOfWork _uow;
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public EventService(IUnitOfWork uow, AppDbContext context, IMapper mapper)
    {
        _uow = uow;
        _context = context;
        _mapper = mapper;
    }

    public async Task<Result<PagedResult<EventListItemDto>>> GetAllAsync(EventQueryParams query, string? userRole)
    {
        await SyncEventStatusesAsync();

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 10 : query.PageSize;

        var eventsQuery = _context.Events
            .Include(e => e.Location)
            .Include(e => e.Organizer)
            .AsQueryable();

        if (!IsStaffOrAdmin(userRole))
            eventsQuery = eventsQuery.Where(e => EventWorkflow.PublicStatuses.Contains(e.Status));

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!IsStaffOrAdmin(userRole) && query.Status is "Draft" or "Cancelled")
                eventsQuery = eventsQuery.Where(_ => false);
            else
                eventsQuery = eventsQuery.Where(e => e.Status == query.Status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            eventsQuery = eventsQuery.Where(e =>
                e.Title.ToLower().Contains(term) ||
                (e.Description != null && e.Description.ToLower().Contains(term)));
        }

        if (query.StartDate.HasValue)
            eventsQuery = eventsQuery.Where(e => e.StartTime >= query.StartDate.Value);

        if (query.EndDate.HasValue)
            eventsQuery = eventsQuery.Where(e => e.StartTime <= query.EndDate.Value);

        var projected = eventsQuery.Select(e => new EventListItemDto
        {
            Id = e.Id,
            Title = e.Title,
            StartTime = e.StartTime,
            EndTime = e.EndTime,
            Status = e.Status,
            LocationName = e.Location.Name,
            OrganizerName = e.Organizer.Name,
            Capacity = e.Capacity,
            RegisteredCount = e.Registrations.Count(r => r.Status != "Cancelled"),
            AvailableSlots = e.Capacity - e.Registrations.Count(r => r.Status != "Cancelled"),
            CreatedAt = e.CreatedAt
        });

        var sortBy = query.SortBy?.ToLower() ?? "createdat";
        var desc = string.IsNullOrWhiteSpace(query.SortDir)
            || !string.Equals(query.SortDir, "asc", StringComparison.OrdinalIgnoreCase);

        projected = sortBy switch
        {
            "title" => desc ? projected.OrderByDescending(e => e.Title) : projected.OrderBy(e => e.Title),
            "status" => desc ? projected.OrderByDescending(e => e.Status) : projected.OrderBy(e => e.Status),
            "availableslots" => desc ? projected.OrderByDescending(e => e.AvailableSlots) : projected.OrderBy(e => e.AvailableSlots),
            "starttime" => desc ? projected.OrderByDescending(e => e.StartTime) : projected.OrderBy(e => e.StartTime),
            _ => desc
                ? projected.OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
                : projected.OrderBy(e => e.CreatedAt).ThenBy(e => e.Id)
        };

        var totalCount = await projected.CountAsync();
        var items = await projected
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Result<PagedResult<EventListItemDto>>.Success(new PagedResult<EventListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<EventDetailDto>> GetByIdAsync(int id, string? userRole)
    {
        var evt = await _context.Events
            .Include(e => e.Location)
            .Include(e => e.Organizer)
            .Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (evt is null)
            return Result<EventDetailDto>.Fail("Event not found.", 404);

        await SyncEventStatusAsync(evt);

        if (!IsStaffOrAdmin(userRole) && !EventWorkflow.PublicStatuses.Contains(evt.Status))
            return Result<EventDetailDto>.Fail("Event not found.", 404);

        return Result<EventDetailDto>.Success(MapToDetailDto(evt));
    }

    public async Task<Result<EventDetailDto>> CreateAsync(CreateEventDto request, int userId)
    {
        var scheduleResult = EventWorkflow.ValidateSchedule(
            request.StartTime, request.EndTime, request.RegistrationDeadline);
        if (!scheduleResult.IsSuccess)
            return Result<EventDetailDto>.Fail(scheduleResult.Error!, scheduleResult.StatusCode);

        if (request.Capacity <= 0)
            return Result<EventDetailDto>.Fail("Capacity must be greater than 0.", 400);

        var locationExists = await _uow.Repository<Location>().AnyAsync(l => l.Id == request.LocationId);
        if (!locationExists)
            return Result<EventDetailDto>.Fail("Location not found.", 400);

        var organizerExists = await _uow.Repository<Organizer>().AnyAsync(o => o.Id == request.OrganizerId);
        if (!organizerExists)
            return Result<EventDetailDto>.Fail("Organizer not found.", 400);

        var evt = new Event
        {
            Title = request.Title,
            Description = request.Description,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            RegistrationDeadline = request.RegistrationDeadline,
            Capacity = request.Capacity,
            LocationId = request.LocationId,
            OrganizerId = request.OrganizerId,
            CreatedById = userId,
            CreatedAt = DateTime.UtcNow,
            Status = nameof(EventStatus.Draft)
        };

        await _uow.Repository<Event>().AddAsync(evt);
        await _uow.SaveChangesAsync();

        var created = await GetByIdAsync(evt.Id, "Staff");
        if (!created.IsSuccess)
            return created;

        return Result<EventDetailDto>.Success(created.Data!, 201);
    }

    public async Task<Result<EventDetailDto>> UpdateAsync(int id, UpdateEventDto request, string userRole)
    {
        var evt = await _context.Events
            .Include(e => e.Location)
            .Include(e => e.Organizer)
            .Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (evt is null)
            return Result<EventDetailDto>.Fail("Event not found.", 404);

        await SyncEventStatusAsync(evt);

        if (evt.Status is nameof(EventStatus.Completed) or nameof(EventStatus.Cancelled)
            && userRole != "Admin")
            return Result<EventDetailDto>.Fail("Only Admin can edit completed or cancelled events.", 403);

        var scheduleResult = EventWorkflow.ValidateSchedule(
            request.StartTime, request.EndTime, request.RegistrationDeadline);
        if (!scheduleResult.IsSuccess)
            return Result<EventDetailDto>.Fail(scheduleResult.Error!, scheduleResult.StatusCode);

        var activeCount = evt.Registrations.Count(r => r.Status != "Cancelled");
        if (request.Capacity < activeCount)
            return Result<EventDetailDto>.Fail(
                $"Capacity cannot be less than active registrations ({activeCount}).", 400);

        if (request.Capacity <= 0)
            return Result<EventDetailDto>.Fail("Capacity must be greater than 0.", 400);

        var locationExists = await _uow.Repository<Location>().AnyAsync(l => l.Id == request.LocationId);
        if (!locationExists)
            return Result<EventDetailDto>.Fail("Location not found.", 400);

        var organizerExists = await _uow.Repository<Organizer>().AnyAsync(o => o.Id == request.OrganizerId);
        if (!organizerExists)
            return Result<EventDetailDto>.Fail("Organizer not found.", 400);

        evt.Title = request.Title;
        evt.Description = request.Description;
        evt.StartTime = request.StartTime;
        evt.EndTime = request.EndTime;
        evt.RegistrationDeadline = request.RegistrationDeadline;
        evt.Capacity = request.Capacity;
        evt.LocationId = request.LocationId;
        evt.OrganizerId = request.OrganizerId;

        EventWorkflow.ApplyResolvedStatus(evt);

        _uow.Repository<Event>().Update(evt);
        await _uow.SaveChangesAsync();

        return Result<EventDetailDto>.Success(MapToDetailDto(evt));
    }

    public async Task<Result<EventDetailDto>> ChangeStatusAsync(int id, string targetStatus, string userRole)
    {
        if (!IsStaffOrAdmin(userRole))
            return Result<EventDetailDto>.Fail("Unauthorized.", 403);

        var evt = await _context.Events
            .Include(e => e.Location)
            .Include(e => e.Organizer)
            .Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (evt is null)
            return Result<EventDetailDto>.Fail("Event not found.", 404);

        await SyncEventStatusAsync(evt);

        var transition = EventWorkflow.ValidateTransition(evt.Status, targetStatus);
        if (!transition.IsSuccess)
            return Result<EventDetailDto>.Fail(transition.Error!, transition.StatusCode);

        evt.Status = targetStatus;
        EventWorkflow.ApplyResolvedStatus(evt);

        _uow.Repository<Event>().Update(evt);
        await _uow.SaveChangesAsync();

        return Result<EventDetailDto>.Success(MapToDetailDto(evt));
    }

    private Task SyncEventStatusesAsync() => EventWorkflow.SyncAllAsync(_context);

    private async Task SyncEventStatusAsync(Event evt)
    {
        var before = evt.Status;
        EventWorkflow.ApplyResolvedStatus(evt);
        if (before != evt.Status)
        {
            _uow.Repository<Event>().Update(evt);
            await _uow.SaveChangesAsync();
        }
    }

    private static bool IsStaffOrAdmin(string? role) =>
        role is "Staff" or "Admin";

    private EventDetailDto MapToDetailDto(Event evt)
    {
        var dto = _mapper.Map<EventDetailDto>(evt);
        var registeredCount = evt.Registrations.Count(r => r.Status != "Cancelled");
        dto.RegisteredCount = registeredCount;
        dto.AvailableSlots = evt.Capacity - registeredCount;
        return dto;
    }
}
