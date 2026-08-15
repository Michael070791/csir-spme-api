using System.Security.Claims;
using Csir.Spme.Application.Common.Interfaces;

namespace Csir.Spme.Api.Auth;

internal sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId => ReadGuid(ClaimTypes.NameIdentifier);

    public Guid? InstituteId => ReadGuid("institute_id");

    public Guid? EmployeeId => ReadGuid("employee_id");

    public string? IdentityType => _httpContextAccessor.HttpContext?.User.FindFirstValue("identity_type");

    public string? IpAddress => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public bool HasPermission(string permissionCode) =>
        _httpContextAccessor.HttpContext?.User.HasClaim("permission", permissionCode) == true;

    public bool IsInRole(string role) => _httpContextAccessor.HttpContext?.User.IsInRole(role) == true;

    private Guid? ReadGuid(string claimType)
    {
        var value = _httpContextAccessor.HttpContext?.User.FindFirstValue(claimType);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
