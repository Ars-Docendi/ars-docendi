namespace Modules.Designaciones.Domain;

/// <summary>Un guard de la máquina de estados rechazó la acción.</summary>
public sealed class ErrorDominioPedido(string mensaje) : Exception(mensaje);

/// <summary>
/// Se intentó crear un pedido para un docente que ya tiene uno vivo en el período
/// [BR-designaciones-001]. Se distingue de <see cref="ErrorDominioPedido"/> porque
/// la API la traduce a 409 y porque el mensaje al usuario NO debe exponer datos del
/// pedido bloqueante: puede pertenecer a una cátedra ajena al actor.
/// </summary>
public sealed class ErrorPedidoDuplicado(string mensaje) : Exception(mensaje);

/// <summary>
/// Ámbito del actor, resuelto UNA vez desde identity antes de invocar la máquina.
/// <para>
/// Resolverlo de antemano es lo que permite que la máquina de estados sea una
/// función pura: los guards de BR-designaciones-009 y BR-designaciones-013 necesitan
/// saber qué roles y qué ámbito tiene el actor, pero no tienen por qué consultarlo.
/// </para>
/// <para>
/// <see cref="RolesDeSistema"/> contiene sólo códigos de roles con <c>es_sistema</c>:
/// un rol creado por el operador agrupa permisos pero no participa del circuito.
/// </para>
/// </summary>
public sealed record ActorContexto(
    Guid UsuarioId,
    IReadOnlySet<string> RolesDeSistema,
    IReadOnlySet<Guid> MateriasACargo,
    IReadOnlySet<Guid> CarrerasACargo)
{
    public bool Tiene(string codigoRol) => RolesDeSistema.Contains(codigoRol);

    /// <summary>Alcance departamental: ve y actúa sobre todo el departamento [BR-designaciones-009].</summary>
    public bool EsDeptoWide =>
        Tiene(RolesCircuito.Secretaria)
        || Tiene(RolesCircuito.Decanato)
        || Tiene(RolesCircuito.Administrativo);
}

/// <summary>
/// Acciones que la máquina sabe aplicar. Unión cerrada mediante records anidados:
/// el <c>switch</c> de <see cref="MaquinaEstadosPedido"/> las cubre exhaustivamente.
/// </summary>
public abstract record AccionPedido
{
    public sealed record Enviar : AccionPedido;

    public sealed record Cancelar : AccionPedido;

    public sealed record Aceptar(string? Comentario = null) : AccionPedido;

    public sealed record Rechazar(string Justificativo) : AccionPedido;

    public sealed record Devolver(string Comentario) : AccionPedido;

    public sealed record Reenviar : AccionPedido;

    public sealed record Priorizar(string Motivo) : AccionPedido;

    public sealed record Despriorizar(string? Comentario = null) : AccionPedido;
}

/// <summary>
/// Qué cambia en el pedido, descrito sin tocarlo. La máquina lo calcula y el
/// servicio lo aplica: así los guards se pueden testear sin base de datos ni EF.
/// <para>
/// <see cref="CodigoRolActuante"/> es el rol concreto con el que el actor ejecutó la
/// acción. No es derivable después —un usuario puede tener varios roles— y por eso
/// la máquina, que es quien decide cuál autorizó la transición, lo devuelve explícito.
/// </para>
/// </summary>
public sealed record TransicionPedido(
    string EstadoResultante,
    string AccionHistorial,
    string CodigoRolActuante,
    string? EtapaRetorno = null,
    string? PropietarioActual = null,
    bool? Prioritario = null,
    string? Comentario = null);
