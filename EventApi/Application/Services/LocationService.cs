using AutoMapper;
using EventApi.Application.DTOs;
using EventApi.Application.Interfaces;
using EventApi.Domain.Common;
using EventApi.Domain.Entities;
using EventApi.Infrastructure.Data;
using EventApi.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventApi.Application.Services;

public class LocationService : ILocationService
{
    private readonly IUnitOfWork _uow;
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public LocationService(IUnitOfWork uow, AppDbContext context, IMapper mapper)
    {
        _uow = uow;
        _context = context;
        _mapper = mapper;
    }

    public async Task<Result<IEnumerable<LocationDto>>> GetAllAsync()
    {
        var items = await _uow.Repository<Location>().Query()
            .OrderBy(l => l.Name)
            .ToListAsync();
        return Result<IEnumerable<LocationDto>>.Success(_mapper.Map<IEnumerable<LocationDto>>(items));
    }

    public async Task<Result<LocationDto>> GetByIdAsync(int id)
    {
        var location = await _uow.Repository<Location>().GetByIdAsync(id);
        if (location is null)
            return Result<LocationDto>.Fail("Location not found.", 404);

        return Result<LocationDto>.Success(_mapper.Map<LocationDto>(location));
    }

    public async Task<Result<LocationDto>> CreateAsync(CreateLocationDto request)
    {
        var capacityResult = ResolveCapacity(request.Capacity);
        if (!capacityResult.IsSuccess)
            return Result<LocationDto>.Fail(capacityResult.Error!, capacityResult.StatusCode);

        var now = DateTime.UtcNow;
        var location = new Location
        {
            Name = request.Name,
            Address = request.Address,
            Description = request.Description,
            Capacity = capacityResult.Data,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _uow.Repository<Location>().AddAsync(location);
        await _uow.SaveChangesAsync();

        return Result<LocationDto>.Success(_mapper.Map<LocationDto>(location), 201);
    }

    public async Task<Result<LocationDto>> UpdateAsync(int id, UpdateLocationDto request)
    {
        var location = await _uow.Repository<Location>().GetByIdAsync(id);
        if (location is null)
            return Result<LocationDto>.Fail("Location not found.", 404);

        if (request.Capacity.HasValue)
        {
            var capacityResult = ResolveCapacity(request.Capacity);
            if (!capacityResult.IsSuccess)
                return Result<LocationDto>.Fail(capacityResult.Error!, capacityResult.StatusCode);
            location.Capacity = capacityResult.Data;
        }

        location.Name = request.Name;
        location.Address = request.Address;
        location.Description = request.Description;
        location.IsActive = request.IsActive;
        location.UpdatedAt = DateTime.UtcNow;

        _uow.Repository<Location>().Update(location);
        await _uow.SaveChangesAsync();

        return Result<LocationDto>.Success(_mapper.Map<LocationDto>(location));
    }

    public async Task<Result<object>> DeleteAsync(int id)
    {
        var location = await _uow.Repository<Location>().GetByIdAsync(id);
        if (location is null)
            return Result<object>.Fail("Location not found.", 404);

        if (await _context.Events.AnyAsync(e => e.LocationId == id))
            return Result<object>.Fail("Cannot delete: location is referenced by events.", 409);

        _uow.Repository<Location>().Remove(location);
        await _uow.SaveChangesAsync();

        return Result<object>.Success(new { message = "Location deleted successfully." });
    }

    private static Result<int> ResolveCapacity(int? capacity)
    {
        if (capacity is null)
            return Result<int>.Success(100);

        if (capacity <= 0)
            return Result<int>.Fail("Capacity must be greater than 0.", 400);

        return Result<int>.Success(capacity.Value);
    }
}
