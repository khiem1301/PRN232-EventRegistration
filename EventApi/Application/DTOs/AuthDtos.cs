namespace EventApi.Application.DTOs;

public class UserDto
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? StudentCode { get; set; }
    public string? Phone { get; set; }
    public string Role { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class RegisterRequestDto
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? StudentCode { get; set; }
    public string? Phone { get; set; }
}

public class LoginRequestDto
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
}

public class LoginResponseDto
{
    public string Token { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; } = null!;
}

public class UpdateProfileDto
{
    public string FullName { get; set; } = null!;
    public string? StudentCode { get; set; }
    public string? Phone { get; set; }
}

public class ChangePasswordDto
{
    public string CurrentPassword { get; set; } = null!;
    public string NewPassword { get; set; } = null!;
}

public class CreateUserDto
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? StudentCode { get; set; }
    public string? Phone { get; set; }
    public string Role { get; set; } = null!;
}

public class UpdateUserDto
{
    public string FullName { get; set; } = null!;
    public string? StudentCode { get; set; }
    public string? Phone { get; set; }
    public string Role { get; set; } = null!;
    public bool IsActive { get; set; }
}
