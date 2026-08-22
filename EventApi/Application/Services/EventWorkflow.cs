using EventApi.Domain.Common;
using EventApi.Domain.Enums;
using EventApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EventApi.Application.Services;

public static class EventWorkflow
{
    public static readonly string[] PublicStatuses =
    [
        nameof(EventStatus.Published),
        nameof(EventStatus.Ongoing),
        nameof(EventStatus.Completed)
    ];

    /// <summary>
    /// Auto-advance by time only. Draft stays Draft until Staff publishes.
    /// Cancelled and Completed are sticky. RegistrationDeadline is not used for status.
    /// Published → Ongoing at StartTime → Completed at EndTime.
    /// </summary>
    public static string ResolveStatusByTime(
        string currentStatus,
        DateTime startTime,
        DateTime endTime,
        DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;

        if (currentStatus is nameof(EventStatus.Draft) or nameof(EventStatus.Cancelled))
            return currentStatus;

        if (currentStatus == nameof(EventStatus.Completed))
            return nameof(EventStatus.Completed);

        if (now >= endTime)
            return nameof(EventStatus.Completed);

        if (now >= startTime)
            return nameof(EventStatus.Ongoing);

        if (currentStatus == nameof(EventStatus.Ongoing))
            return nameof(EventStatus.Ongoing);

        return nameof(EventStatus.Published);
    }

    public static void ApplyResolvedStatus(
        Domain.Entities.Event evt,
        DateTime? utcNow = null)
    {
        evt.Status = ResolveStatusByTime(
            evt.Status,
            evt.StartTime,
            evt.EndTime,
            utcNow);
    }

    public static Result<object> ValidateTransition(string current, string target)
    {
        var allowed = (current, target) switch
        {
            (nameof(EventStatus.Draft), nameof(EventStatus.Published)) => true,
            (nameof(EventStatus.Published), nameof(EventStatus.Ongoing)) => true,
            (nameof(EventStatus.Ongoing), nameof(EventStatus.Completed)) => true,
            (nameof(EventStatus.Draft), nameof(EventStatus.Cancelled)) => true,
            (nameof(EventStatus.Published), nameof(EventStatus.Cancelled)) => true,
            (nameof(EventStatus.Ongoing), nameof(EventStatus.Cancelled)) => true,
            _ => false
        };

        if (!allowed)
            return Result<object>.Fail($"Cannot change status from {current} to {target}.", 409);

        return Result<object>.Success(new { });
    }

    public static async Task SyncAllAsync(AppDbContext context)
    {
        var events = await context.Events.ToListAsync();
        var changed = false;

        foreach (var evt in events)
        {
            var before = evt.Status;
            ApplyResolvedStatus(evt);
            if (before != evt.Status)
                changed = true;
        }

        if (changed)
            await context.SaveChangesAsync();
    }

    public static Result<object> ValidateSchedule(DateTime startTime, DateTime endTime, DateTime registrationDeadline)
    {
        if (endTime <= startTime)
            return Result<object>.Fail("EndTime must be after StartTime.", 400);

        if (registrationDeadline > startTime)
            return Result<object>.Fail("RegistrationDeadline must be on or before StartTime.", 400);

        return Result<object>.Success(new { });
    }
}
