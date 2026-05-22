using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace ArsDocendi.Shared.Auth;

internal sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public string? UserId => Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);
    public IReadOnlyList<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? [];
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;
}
