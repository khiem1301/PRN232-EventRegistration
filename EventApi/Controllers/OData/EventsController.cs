using EventApi.Application.DTOs;
using EventApi.Application.Services;
using EventApi.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace EventApi.Controllers.OData;

[Authorize(Roles = "Staff,Admin")]
public class EventsController : ODataController
{
    private readonly AppDbContext _context;

    public EventsController(AppDbContext context) => _context = context;

    [EnableQuery(PageSize = 20, AllowedQueryOptions = AllowedQueryOptions.All)]
    public async Task<IQueryable<EventODataDto>> Get()
    {
        await EventWorkflow.SyncAllAsync(_context);

        return _context.Events.Select(e => new EventODataDto
        {
            Id = e.Id,
            Title = e.Title,
            StartTime = e.StartTime,
            EndTime = e.EndTime,
            Status = e.Status,
            Capacity = e.Capacity,
            RegisteredCount = e.Registrations.Count(r => r.Status != "Cancelled"),
            AvailableSlots = e.Capacity - e.Registrations.Count(r => r.Status != "Cancelled"),
            LocationName = e.Location.Name,
            OrganizerName = e.Organizer.Name
        });
    }
}
