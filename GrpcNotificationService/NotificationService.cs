using EventApi.Protos;
using Grpc.Core;

namespace GrpcNotificationService;

public sealed class NotificationService : EventApi.Protos.NotificationService.NotificationServiceBase
{
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ILogger<NotificationService> logger) => _logger = logger;

    public override Task<RegistrationNotificationReply> SendRegistrationConfirmation(
        RegistrationNotificationRequest request,
        ServerCallContext context)
    {
        _logger.LogInformation(
            "Registration notification received: {UserName} ({Email}) registered for event {EventId} - {EventTitle}",
            request.UserName,
            request.UserEmail,
            request.EventId,
            request.EventTitle);

        return Task.FromResult(new RegistrationNotificationReply
        {
            Success = true,
            Message = $"Registration notification sent for {request.EventTitle}."
        });
    }
}
