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

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _uow;
    private readonly JwtTokenService _jwtTokenService;
    private readonly IMapper _mapper;

    public AuthService(IUnitOfWork uow, JwtTokenService jwtTokenService, IMapper mapper)
    {
        _uow = uow;
        _jwtTokenService = jwtTokenService;
        _mapper = mapper;
    }

    public async Task<Result<UserDto>> RegisterAsync(RegisterRequestDto request)
    {
        var repo = _uow.Repository<User>();
        if (await repo.AnyAsync(u => u.Email == request.Email))
            return Result<UserDto>.Fail("Email already exists.", 409);

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
            Role = UserRole.Student,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        await repo.AddAsync(user);
        await _uow.SaveChangesAsync();

        return Result<UserDto>.Success(_mapper.Map<UserDto>(user), 201);
    }

    public async Task<Result<LoginResponseDto>> LoginAsync(LoginRequestDto request)
    {
        var user = await _uow.Repository<User>().Query()
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user is null || !PasswordHasher.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
            return Result<LoginResponseDto>.Fail("Invalid email or password.", 401);

        if (!user.IsActive)
            return Result<LoginResponseDto>.Fail("Account is inactive.", 403);

        var userDto = _mapper.Map<UserDto>(user);
        var response = _jwtTokenService.GenerateToken(user, userDto);
        return Result<LoginResponseDto>.Success(response);
    }
}
