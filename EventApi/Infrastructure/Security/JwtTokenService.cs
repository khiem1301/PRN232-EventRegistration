using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EventApi.Application.DTOs;
using EventApi.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace EventApi.Infrastructure.Security;

public class JwtTokenService(IConfiguration configuration)
{
    public LoginResponseDto GenerateToken(User user, UserDto userDto)
    {
        var jwtSettings = configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
        var expireMinutes = int.Parse(jwtSettings["ExpireMinutes"] ?? "60");
        var expiresAt = DateTime.UtcNow.AddMinutes(expireMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new LoginResponseDto
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAt = expiresAt,
            User = userDto
        };
    }
}
