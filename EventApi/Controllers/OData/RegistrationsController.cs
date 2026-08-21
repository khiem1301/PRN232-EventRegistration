using EventApi.Application.DTOs;
using EventApi.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace EventApi.Controllers.OData;

[Authorize(Roles = "Staff,Admin")]
public class RegistrationsController : ODataController
{
    private readonly AppDbContext _context;

    public RegistrationsController(AppDbContext context) => _context = context;

    [EnableQuery(PageSize = 50, AllowedQueryOptions = AllowedQueryOptions.All)]
    public IQueryable<RegistrationODataDto> Get() =>
        _context.Registrations.Select(registration => new RegistrationODataDto
        {
            Id = registration.Id,
            EventId = registration.EventId,
            EventTitle = registration.Event.Title,
            StudentId = registration.StudentId,
            StudentName = registration.Student.FullName,
            Status = registration.Status,
            RegisteredAt = registration.RegisteredAt,
            CheckedInAt = registration.CheckedInAt,
            CancelledAt = registration.CancelledAt
        });
}
