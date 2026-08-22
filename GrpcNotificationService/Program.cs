using GrpcNotificationService;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddGrpc();

var app = builder.Build();
app.MapGrpcService<NotificationService>();
app.MapGet("/", () => "gRPC notification service is running.");
app.Run();
