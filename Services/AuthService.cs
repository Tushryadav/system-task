using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SRAAS.Api.Data;
using SRAAS.Api.DTOs;
using SRAAS.Api.Entities;
using SRAAS.Api.Enums;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SRAAS.Api.Services;

public interface IAuthService
{
    Task<AuthResponse?> LoginAsync(LoginRequest request);
    Task<AuthResponse?> RefreshAsync(string rawRefreshToken);
    Task LogoutAsync(Guid memberId);
    string GenerateJwt(OrgMember member);
}

public class AuthService : IAuthService
{
    private readonly SraasDbContext _db;
    private readonly IPasswordService _passwordService;
    private readonly IAuditService _audit;
    private readonly IConfiguration _config;

    public AuthService(
        SraasDbContext db,
        IPasswordService passwordService,
        IAuditService audit,
        IConfiguration config)
    {
        _db = db;
        _passwordService = passwordService;
        _audit = audit;
        _config = config;
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var member = await _db.OrgMembers
            .Include(m => m.Organization)
            .FirstOrDefaultAsync(m =>
                m.Email == request.Email &&
                m.Organization.Slug == request.OrgSlug &&
                m.IsActive);

        if (member == null || !_passwordService.Verify(request.Password, member.PasswordHash))
        {
            // Log failed attempt if org exists
            var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Slug == request.OrgSlug);
            if (org != null)
            {
                await _audit.LogAsync(org.Id, null, "auth.login_failed",
                    metadata: new { email = request.Email });
            }
            return null;
        }

        var accessToken = GenerateJwt(member);
        var rawRefreshToken = GenerateSecureToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            MemberId = member.Id,
            OrgId = member.OrgId,
            TokenHash = ComputeSha256(rawRefreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        });

        await _db.SaveChangesAsync();

        await _audit.LogAsync(member.OrgId, member.Id, "auth.login",
            targetType: "member", targetId: member.Id);

        return new AuthResponse(accessToken, rawRefreshToken);
    }

    public async Task<AuthResponse?> RefreshAsync(string rawRefreshToken)
    {
        var tokenHash = ComputeSha256(rawRefreshToken);

        var stored = await _db.RefreshTokens
            .Include(t => t.Member)
            .FirstOrDefaultAsync(t =>
                t.TokenHash == tokenHash &&
                !t.IsRevoked &&
                t.ExpiresAt > DateTime.UtcNow);

        if (stored == null)
            return null;

        // Rotate — delete old, issue new
        using var tx = await _db.Database.BeginTransactionAsync();

        _db.RefreshTokens.Remove(stored);

        var newRawToken = GenerateSecureToken();
        _db.RefreshTokens.Add(new RefreshToken
        {
            MemberId = stored.MemberId,
            OrgId = stored.OrgId,
            TokenHash = ComputeSha256(newRawToken),
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        });

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        var newAccessToken = GenerateJwt(stored.Member);

        return new AuthResponse(newAccessToken, newRawToken);
    }

    public async Task LogoutAsync(Guid memberId)
    {
        await _db.RefreshTokens
            .Where(t => t.MemberId == memberId && !t.IsRevoked)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRevoked, true));

        var member = await _db.OrgMembers.FindAsync(memberId);
        if (member != null)
        {
            await _audit.LogAsync(member.OrgId, member.Id, "auth.logout",
                targetType: "member", targetId: member.Id);
        }
    }

    public string GenerateJwt(OrgMember member)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, member.Id.ToString()),
            new Claim("org_id", member.OrgId.ToString()),
            new Claim("role", member.Role.ToString().ToLower()),
            new Claim(JwtRegisteredClaimNames.Email, member.Email),
            new Claim(JwtRegisteredClaimNames.Name, member.Name),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLower();
    }

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }
}
