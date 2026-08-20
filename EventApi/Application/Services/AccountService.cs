using AutoMapper;
using EventApi.Application.DTOs;
using EventApi.Application.Interfaces;
using EventApi.Domain.Common;
using EventApi.Domain.Entities;
using EventApi.Infrastructure.Repositories;
using EventApi.Infrastructure.Security;

namespace EventApi.Application.Services;

public class AccountService : IAccountService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public AccountService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<Result<UserDto>> GetMeAsync(int userId)
    {
        var user = await _uow.Repository<User>().GetByIdAsync(userId);
        if (user is null)
            return Result<UserDto>.Fail("User not found.", 404);

        return Result<UserDto>.Success(_mapper.Map<UserDto>(user));
    }

    public async Task<Result<UserDto>> UpdateMeAsync(int userId, UpdateProfileDto request)
    {
        var user = await _uow.Repository<User>().GetByIdAsync(userId);
        if (user is null)
            return Result<UserDto>.Fail("User not found.", 404);

        user.FullName = request.FullName;
        user.StudentCode = request.StudentCode;
        user.Phone = request.Phone;
        user.UpdatedAt = DateTime.UtcNow;

        _uow.Repository<User>().Update(user);
        await _uow.SaveChangesAsync();

        return Result<UserDto>.Success(_mapper.Map<UserDto>(user));
    }

    public async Task<Result<object>> ChangePasswordAsync(int userId, ChangePasswordDto request)
    {
        var user = await _uow.Repository<User>().GetByIdAsync(userId);
        if (user is null)
            return Result<object>.Fail("User not found.", 404);

        if (!PasswordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash, user.PasswordSalt))
            return Result<object>.Fail("Current password is incorrect.", 400);

        var (hash, salt) = PasswordHasher.HashPassword(request.NewPassword);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        user.UpdatedAt = DateTime.UtcNow;

        _uow.Repository<User>().Update(user);
        await _uow.SaveChangesAsync();

        return Result<object>.Success(new { message = "Password changed successfully." });
    }
}
