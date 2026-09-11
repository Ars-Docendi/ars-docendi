namespace ArsDocendi.Shared.Auth;

public interface ICurrentUser
{
    string? UserId { get; }
    string? Email { get; }
    IReadOnlyList<string> Roles { get; }
    IReadOnlyList<string> Permissions => [];
    bool IsAuthenticated { get; }
}
