namespace ArsDocendi.Shared.Auth;

/// <summary>Claims propios de la sesión que también leen los servicios compartidos.</summary>
public static class ClaimsSesion
{
    /// <summary>Asignación de rol (<c>identity.user_roles.id</c>) con la que opera la sesión.</summary>
    public const string Asignacion = "ars:asignacion";

    /// <summary>Presente cuando la persona eligió la asignación; si falta, opera con la de por defecto.</summary>
    public const string AsignacionElegida = "ars:asignacion-elegida";
}
