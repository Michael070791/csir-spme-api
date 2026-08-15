using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Csir.Spme.Domain.Iam;
using Microsoft.AspNetCore.Identity;
using Csir.Spme.Domain.Constants;
using Microsoft.IdentityModel.Tokens;

namespace Csir.Spme.Api.Auth;

public interface IJwtTokenService
{
    Task<IssuedToken> CreateAccessTokenAsync(User user, Guid sessionId, CancellationToken ct);
}

public sealed record IssuedToken(string AccessToken, DateTimeOffset ExpiresAt, int ExpiresInSeconds);

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;

    public JwtTokenService(IConfiguration configuration, UserManager<User> userManager, RoleManager<Role> roleManager)
    {
        _configuration = configuration;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<IssuedToken> CreateAccessTokenAsync(User user, Guid sessionId, CancellationToken ct)
    {
        var jwtSection = _configuration.GetSection("Jwt");
        var issuer = jwtSection.GetValue<string>("Issuer") ?? "csir-spme-api";
        var audience = jwtSection.GetValue<string>("Audience") ?? "csir-spme-client";
        var key = SecretConfiguration.RequireStrongSecret(_configuration, "Jwt:Key");
        var expiryMinutes = jwtSection.GetValue("ExpiryMinutes", 60);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new("sid", sessionId.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new("security_stamp", user.SecurityStamp ?? string.Empty),
            new("identity_type", user.IdentityType)
        };

        if (user.EmployeeId.HasValue)
        {
            claims.Add(new Claim("employee_id", user.EmployeeId.Value.ToString()));
            claims.Add(new Claim("self", $"Self:{user.EmployeeId.Value}"));
        }

        if (user.InstituteId.HasValue)
            claims.Add(new Claim("institute_id", user.InstituteId.Value.ToString()));

        var roles = await _userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        foreach (var role in roles)
        {
            var identityRole = await _roleManager.FindByNameAsync(role);
            if (identityRole is null) continue;
            var roleClaims = await _roleManager.GetClaimsAsync(identityRole);
            claims.AddRange(roleClaims.Where(claim => claim.Type == "permission"));
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new IssuedToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt,
            (int)TimeSpan.FromMinutes(expiryMinutes).TotalSeconds);
    }
}
