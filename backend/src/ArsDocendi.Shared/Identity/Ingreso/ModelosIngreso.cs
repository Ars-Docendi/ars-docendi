namespace ArsDocendi.Shared.Identity.Ingreso;

/// <summary>
/// Datos de la cuenta Microsoft tomados del id_token ya validado. <see cref="Email"/>
/// es mutable y sólo sirve para encontrar al usuario; <see cref="EmailDominioVerificado"/>
/// es el claim <c>xms_edov</c>, ausente en algunos tipos de cuenta.
/// </summary>
public sealed record DatosCuentaMicrosoft(
    Guid TenantId,
    Guid ObjectId,
    string? Email,
    bool? EmailDominioVerificado);

public enum MotivoRechazoIngreso
{
    MailNoVerificado,
    NoRegistrado,
    Inactivo,
    SinRol,
    /// <summary>El mail corresponde a un usuario ya vinculado a otra cuenta Microsoft.</summary>
    CuentaDistinta,
}

/// <summary>Resultado de la regla de ingreso: un usuario aceptado o un motivo de rechazo.</summary>
public sealed record ResultadoIngreso(Guid? UsuarioId, MotivoRechazoIngreso? Motivo)
{
    public bool Aceptado => UsuarioId is not null;

    public static ResultadoIngreso Aceptar(Guid usuarioId) => new(usuarioId, null);
    public static ResultadoIngreso Rechazar(MotivoRechazoIngreso motivo) => new(null, motivo);
}

/// <summary>
/// Identidad efectiva de una sesión: el rol con el que opera y sus permisos, leídos
/// de la persistencia en cada solicitud.
/// </summary>
public sealed record IdentidadSesion(
    Guid UsuarioId,
    string NombreParaMostrar,
    string Upn,
    string RolCodigo,
    string RolNombre,
    IReadOnlyList<string> Permisos,
    int CantidadRoles);
