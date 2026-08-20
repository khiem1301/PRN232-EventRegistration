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
    /// Trạng thái tự động theo thời gian đã nhập khi tạo/sửa event (UTC).
    /// Draft: trước hạn đăng ký | Published: từ hạn ĐK đến StartTime | Ongoing | Completed
    /// </summary>
    public static string ResolveStatusByTime(
        DateTime startTime,
        DateTime endTime,
        DateTime registrationDeadline,
        DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;

        if (now >= endTime)
            return nameof(EventStatus.Completed);

        if (now >= startTime)
            return nameof(EventStatus.Ongoing);

        if (now >= registrationDeadline)
            return nameof(EventStatus.Published);

        return nameof(EventStatus.Draft);
    }

    public static void ApplyResolvedStatus(
        Domain.Entities.Event evt,
        DateTime? utcNow = null)
    {
        evt.Status = ResolveStatusByTime(
            evt.StartTime,
            evt.EndTime,
            evt.RegistrationDeadline,
            utcNow);
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
