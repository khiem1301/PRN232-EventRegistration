using EventApi.Application.DTOs;
using EventApi.Application.Interfaces;
using EventApi.Domain.Common;
using EventApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EventApi.Application.Services;

public class ReportService : IReportService
{
    private readonly AppDbContext _context;

    public ReportService(AppDbContext context) => _context = context;

    public async Task<Result<EventReportDto>> GetEventReportAsync(int eventId)
    {
        await EventWorkflow.SyncAllAsync(_context);

        var evt = await _context.Events
            .Include(e => e.Registrations)
            .Include(e => e.Feedbacks)
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (evt is null)
            return Result<EventReportDto>.Fail("Event not found.", 404);

        return Result<EventReportDto>.Success(BuildEventReport(evt));
    }

    public async Task<Result<PagedResult<EventReportDto>>> GetEventsReportAsync(string? status, int page, int pageSize)
    {
        await EventWorkflow.SyncAllAsync(_context);

        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 10 : pageSize;

        var query = _context.Events
            .Include(e => e.Registrations)
            .Include(e => e.Feedbacks)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(e => e.Status == status);

        var totalCount = await query.CountAsync();
        var events = await query
            .OrderByDescending(e => e.StartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Result<PagedResult<EventReportDto>>.Success(new PagedResult<EventReportDto>
        {
            Items = events.Select(BuildEventReport),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<OverviewReportDto>> GetOverviewAsync()
    {
        await EventWorkflow.SyncAllAsync(_context);

        var events = await _context.Events
            .Include(e => e.Registrations)
            .Include(e => e.Feedbacks)
            .ToListAsync();

        var totalRegistrations = events.Sum(e => e.Registrations.Count(r => r.Status != "Cancelled"));
        var totalCheckedIn = events.Sum(e =>
            e.Registrations.Count(r => r.Status != "Cancelled" && r.CheckedInAt != null));
        var totalCapacity = events.Sum(e => e.Capacity);
        var allFeedbacks = events.SelectMany(e => e.Feedbacks).ToList();

        return Result<OverviewReportDto>.Success(new OverviewReportDto
        {
            TotalEvents = events.Count,
            EventsByStatus = events
                .GroupBy(e => e.Status)
                .ToDictionary(g => g.Key, g => g.Count()),
            TotalRegistrations = totalRegistrations,
            TotalCheckedIn = totalCheckedIn,
            OverallAttendanceRate = CalcRate(totalCheckedIn, totalRegistrations),
            OverallFillRate = CalcRate(totalRegistrations, totalCapacity),
            OverallAverageRating = allFeedbacks.Count > 0
                ? allFeedbacks.Average(f => f.Rating)
                : null
        });
    }

    private static EventReportDto BuildEventReport(Domain.Entities.Event evt)
    {
        var registeredCount = evt.Registrations.Count(r => r.Status != "Cancelled");
        var checkedInCount = evt.Registrations.Count(r =>
            r.Status != "Cancelled" && r.CheckedInAt != null);
        var feedbackCount = evt.Feedbacks.Count;

        return new EventReportDto
        {
            EventId = evt.Id,
            Title = evt.Title,
            Status = evt.Status,
            Capacity = evt.Capacity,
            RegisteredCount = registeredCount,
            CheckedInCount = checkedInCount,
            AttendanceRate = CalcRate(checkedInCount, registeredCount),
            FillRate = CalcRate(registeredCount, evt.Capacity),
            AverageRating = feedbackCount > 0 ? evt.Feedbacks.Average(f => f.Rating) : null,
            FeedbackCount = feedbackCount
        };
    }

    private static double CalcRate(int numerator, int denominator) =>
        denominator == 0 ? 0 : (double)numerator / denominator;
}
