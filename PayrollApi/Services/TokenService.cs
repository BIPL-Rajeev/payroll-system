using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using PayrollApi.Core.Entities;

namespace PayrollApi.Services;

/// <summary>
/// Issues JWT access tokens for authenticated users.
/// Configuration read from section "Jwt": SecretKey / Issuer / Audience / ExpiryMinutes.
/// </summary>
public sealed class TokenService
{
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expiryMinutes;

    public TokenService(IConfiguration configuration)
    {
        _secretKey = configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");

        _issuer = configuration["Jwt:Issuer"] ?? "PayrollApi";
        _audience = configuration["Jwt:Audience"] ?? "PayrollWeb";
        _expiryMinutes = int.TryParse(configuration["Jwt:ExpiryMinutes"], out int minutes)
            ? minutes
            : 60;
    }

    public string CreateToken(User user)
    {
        byte[] key = Encoding.UTF8.GetBytes(_secretKey);
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role),
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(_expiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
