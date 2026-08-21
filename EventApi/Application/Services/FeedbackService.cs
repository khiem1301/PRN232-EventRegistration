using EventApi.Application.DTOs;
using EventApi.Application.Interfaces;
using EventApi.Domain.Common;
using EventApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EventApi.Application.Services;

public class FeedbackService : IFeedbackService
{
    private readonly AppDbContext _context;

    public FeedbackService(AppDbContext context) => _context = context;

    public async Task<Result<IEnumerable<FeedbackDto>>> GetEventFeedbacksAsync(int eventId)
    {
        var items = await _context.Feedbacks
            .AsNoTracking()
            .Include(f => f.Event)
            .Include(f => f.Student)
            .Where(f => f.EventId == eventId)
            .OrderByDescending(f => f.SubmittedAt)
            .ToListAsync();

        return Result<IEnumerable<FeedbackDto>>.Success(items.Select(MapToDto));
    }

    public async Task<Result<IEnumerable<FeedbackDto>>> GetMyFeedbacksAsync(int studentId)
    {
        var items = await _context.Feedbacks
            .AsNoTracking()
            .Include(f => f.Event)
            .Include(f => f.Student)
            .Where(f => f.StudentId == studentId)
            .OrderByDescending(f => f.SubmittedAt)
            .ToListAsync();

        return Result<IEnumerable<FeedbackDto>>.Success(items.Select(MapToDto));
    }

    public async Task<Result<FeedbackDto>> SubmitAsync(int studentId, CreateFeedbackDto request)
    {
        if (request.Rating is < 1 or > 5)
            return Result<FeedbackDto>.Fail("Rating must be between 1 and 5.", 400);

        var evt = await _context.Events
            .FirstOrDefaultAsync(e => e.Id == request.EventId);

        if (evt is null)
            return Result<FeedbackDto>.Fail("Event not found.", 404);

        var attended = await _context.Registrations.AnyAsync(r =>
            r.EventId == request.EventId &&
            r.StudentId == studentId &&
            r.Status == "Attended");

        if (!attended)
            return Result<FeedbackDto>.Fail("You can only submit feedback after attending the event.", 400);

        var alreadySubmitted = await _context.Feedbacks.AnyAsync(f =>
            f.EventId == request.EventId && f.StudentId == studentId);

        if (alreadySubmitted)
            return Result<FeedbackDto>.Fail("You already submitted feedback for this event.", 400);

        var feedback = new EventApi.Domain.Entities.Feedback
        {
            EventId = request.EventId,
            StudentId = studentId,
            Rating = request.Rating,
            Comment = request.Comment,
            SubmittedAt = DateTime.UtcNow
        };

        _context.Feedbacks.Add(feedback);
        await _context.SaveChangesAsync();

        return Result<FeedbackDto>.Success(MapToDto(feedback));
    }

    private static FeedbackDto MapToDto(EventApi.Domain.Entities.Feedback feedback) => new()
    {
        Id = feedback.Id,
        EventId = feedback.EventId,
        EventTitle = feedback.Event?.Title ?? string.Empty,
        StudentId = feedback.StudentId,
        StudentName = feedback.Student?.FullName ?? string.Empty,
        Rating = feedback.Rating,
        Comment = feedback.Comment,
        SubmittedAt = feedback.SubmittedAt
    };
}
