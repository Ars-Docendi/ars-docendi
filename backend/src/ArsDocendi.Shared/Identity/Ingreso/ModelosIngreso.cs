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
/// Una asignación de rol vigente tal como se ofrece al elegir con qué rol operar:
/// el rol y su ámbito (<c>global</c>, <c>materia</c> o <c>carrera</c>).
/// </summary>
public sealed record AsignacionSesion(
    Guid Id,
    string RolCodigo,
    string RolNombre,
    string AmbitoTipo,
    string? AmbitoNombre);

/// <summary>
/// Identidad efectiva de una sesión, leída de la persistencia en cada solicitud: la
/// asignación con la que opera y los permisos de su rol. Mientras la persona no eligió
/// (<see cref="AsignacionElegida"/> falso) opera con la primera por nombre de rol y
/// ámbito, y el frontend le ofrece elegir.
/// </summary>
public sealed record IdentidadSesion(
    Guid UsuarioId,
    string NombreParaMostrar,
    string Upn,
    AsignacionSesion Asignacion,
    bool AsignacionElegida,
    IReadOnlyList<string> Permisos,
    IReadOnlyList<AsignacionSesion> Asignaciones);
