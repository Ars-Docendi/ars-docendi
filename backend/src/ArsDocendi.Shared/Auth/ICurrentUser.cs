namespace ArsDocendi.Shared.Auth;

public interface ICurrentUser
{
    string? UserId { get; }
    string? Email { get; }
    IReadOnlyList<string> Roles { get; }
    IReadOnlyList<string> Permissions => [];
    /// <summary>Asignación de rol elegida por la sesión; <c>null</c> fuera de una solicitud o sin elegir.</summary>
    Guid? AsignacionActivaId => null;
    bool IsAuthenticated { get; }
}
