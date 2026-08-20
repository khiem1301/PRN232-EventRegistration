using AutoMapper;
using EventApi.Application.DTOs;
using EventApi.Application.Interfaces;
using EventApi.Domain.Common;
using EventApi.Domain.Entities;
using EventApi.Domain.Enums;
using EventApi.Infrastructure.Repositories;
using EventApi.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace EventApi.Application.Services;

public class UserService : IUserService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public UserService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<Result<PagedResult<UserDto>>> GetAllAsync(string? search, int page, int pageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 10 : pageSize;

        var query = _uow.Repository<User>().Query();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u =>
                u.Email.ToLower().Contains(term) ||
                u.FullName.ToLower().Contains(term) ||
                (u.StudentCode != null && u.StudentCode.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var result = new PagedResult<UserDto>
        {
            Items = _mapper.Map<IEnumerable<UserDto>>(items),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return Result<PagedResult<UserDto>>.Success(result);
    }

    public async Task<Result<UserDto>> GetByIdAsync(int id)
    {
        var user = await _uow.Repository<User>().GetByIdAsync(id);
        if (user is null)
            return Result<UserDto>.Fail("User not found.", 404);

        return Result<UserDto>.Success(_mapper.Map<UserDto>(user));
    }

    public async Task<Result<UserDto>> CreateAsync(CreateUserDto request)
    {
        var repo = _uow.Repository<User>();
        if (await repo.AnyAsync(u => u.Email == request.Email))
            return Result<UserDto>.Fail("Email already exists.", 409);

        if (!Enum.TryParse<UserRole>(request.Role, out var role))
            return Result<UserDto>.Fail("Invalid role.", 400);

        var (hash, salt) = PasswordHasher.HashPassword(request.Password);
        var now = DateTime.UtcNow;

        var user = new User
        {
            Email = request.Email,
            PasswordHash = hash,
            PasswordSalt = salt,
            FullName = request.FullName,
            StudentCode = request.StudentCode,
            Phone = request.Phone,
            Role = role,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        await repo.AddAsync(user);
        await _uow.SaveChangesAsync();

        return Result<UserDto>.Success(_mapper.Map<UserDto>(user), 201);
    }

    public async Task<Result<UserDto>> UpdateAsync(int id, UpdateUserDto request)
    {
        var user = await _uow.Repository<User>().GetByIdAsync(id);
        if (user is null)
            return Result<UserDto>.Fail("User not found.", 404);

        if (!Enum.TryParse<UserRole>(request.Role, out var role))
            return Result<UserDto>.Fail("Invalid role.", 400);

        user.FullName = request.FullName;
        user.StudentCode = request.StudentCode;
        user.Phone = request.Phone;
        user.Role = role;
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        _uow.Repository<User>().Update(user);
        await _uow.SaveChangesAsync();

        return Result<UserDto>.Success(_mapper.Map<UserDto>(user));
    }

    public async Task<Result<object>> DeactivateAsync(int id)
    {
        var user = await _uow.Repository<User>().GetByIdAsync(id);
        if (user is null)
            return Result<object>.Fail("User not found.", 404);

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;

        _uow.Repository<User>().Update(user);
        await _uow.SaveChangesAsync();

        return Result<object>.Success(new { message = "User deactivated successfully." });
    }
}
