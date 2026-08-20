using AutoMapper;
using EventApi.Application.DTOs;
using EventApi.Application.Interfaces;
using EventApi.Domain.Common;
using EventApi.Domain.Entities;
using EventApi.Infrastructure.Data;
using EventApi.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventApi.Application.Services;

public class OrganizerService : IOrganizerService
{
    private readonly IUnitOfWork _uow;
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public OrganizerService(IUnitOfWork uow, AppDbContext context, IMapper mapper)
    {
        _uow = uow;
        _context = context;
        _mapper = mapper;
    }

    public async Task<Result<IEnumerable<OrganizerDto>>> GetAllAsync()
    {
        var items = await _uow.Repository<Organizer>().Query()
            .OrderBy(o => o.Name)
            .ToListAsync();
        return Result<IEnumerable<OrganizerDto>>.Success(_mapper.Map<IEnumerable<OrganizerDto>>(items));
    }

    public async Task<Result<OrganizerDto>> GetByIdAsync(int id)
    {
        var organizer = await _uow.Repository<Organizer>().GetByIdAsync(id);
        if (organizer is null)
            return Result<OrganizerDto>.Fail("Organizer not found.", 404);

        return Result<OrganizerDto>.Success(_mapper.Map<OrganizerDto>(organizer));
    }

    public async Task<Result<OrganizerDto>> CreateAsync(CreateOrganizerDto request)
    {
        var now = DateTime.UtcNow;
        var organizer = new Organizer
        {
            Name = request.Name,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            Description = request.Description,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _uow.Repository<Organizer>().AddAsync(organizer);
        await _uow.SaveChangesAsync();

        return Result<OrganizerDto>.Success(_mapper.Map<OrganizerDto>(organizer), 201);
    }

    public async Task<Result<OrganizerDto>> UpdateAsync(int id, UpdateOrganizerDto request)
    {
        var organizer = await _uow.Repository<Organizer>().GetByIdAsync(id);
        if (organizer is null)
            return Result<OrganizerDto>.Fail("Organizer not found.", 404);

        organizer.Name = request.Name;
        organizer.ContactEmail = request.ContactEmail;
        organizer.ContactPhone = request.ContactPhone;
        organizer.Description = request.Description;
        organizer.IsActive = request.IsActive;
        organizer.UpdatedAt = DateTime.UtcNow;

        _uow.Repository<Organizer>().Update(organizer);
        await _uow.SaveChangesAsync();

        return Result<OrganizerDto>.Success(_mapper.Map<OrganizerDto>(organizer));
    }

    public async Task<Result<object>> DeleteAsync(int id)
    {
        var organizer = await _uow.Repository<Organizer>().GetByIdAsync(id);
        if (organizer is null)
            return Result<object>.Fail("Organizer not found.", 404);

        if (await _context.Events.AnyAsync(e => e.OrganizerId == id))
            return Result<object>.Fail("Cannot delete: organizer is referenced by events.", 409);

        _uow.Repository<Organizer>().Remove(organizer);
        await _uow.SaveChangesAsync();

        return Result<object>.Success(new { message = "Organizer deleted successfully." });
    }
}
