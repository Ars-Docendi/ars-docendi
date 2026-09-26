using System.ComponentModel.DataAnnotations;
using Modules.Asistente.Application;

namespace Modules.Asistente.Api;

/// <summary>Una conversación propia, en la lista.</summary>
/// <param name="Archivada">design.md D3 de asistente-historial-conversaciones.</param>
/// <param name="PendienteDeBorrado">
/// design.md D4. Siempre falso del lado propio (<see cref="HistorialController"/>
/// nunca lista una conversación pendiente); en verdad únicamente en la
/// lectura de soporte (<see cref="SoporteHistorialController"/>,
/// asistente-acceso-de-soporte-al-historial).
/// </param>
public sealed record ConversacionResumenDto(
    Guid Id,
    string Titulo,
    DateTimeOffset CreadoEn,
    DateTimeOffset UltimaActividad,
    bool Archivada,
    bool PendienteDeBorrado)
{
    internal static ConversacionResumenDto De(ConversacionResumen c) =>
        new(c.Id, c.Titulo, c.CreadoEn, c.UltimaActividad, c.Archivada, c.PendienteDeBorrado);
}

/// <summary>
/// Lo que devuelve un borrado — uno o todos —: el id del lote, para poder
/// deshacerlo dentro de su ventana (design.md D4).
/// </summary>
public sealed record LoteDeBorradoDto(Guid LoteDeBorrado);

/// <summary>Un turno de una conversación propia.</summary>
/// <param name="Sql">
/// Presente solo con <c>asistente.ver_consulta</c> (design.md D9 de
/// asistente-historial-conversaciones) — mismo criterio que el campo
/// homónimo del turno en vivo.
/// </param>
public sealed record TurnoDeHistorialDto(
    Guid Id, string Pregunta, string? Sql, string Estado, DateTimeOffset OcurrioEn)
{
    internal static TurnoDeHistorialDto De(TurnoDeHistorial t, bool veLaConsulta) =>
        new(t.Id, t.Pregunta, veLaConsulta ? t.SqlResuelto : null, NombreDelEstado.De(t.Estado), t.OcurrioEn);
}

/// <summary>Una conversación propia, con sus turnos.</summary>
public sealed record ConversacionDetalleDto(
    Guid Id,
    string Titulo,
    DateTimeOffset CreadoEn,
    DateTimeOffset UltimaActividad,
    IReadOnlyList<TurnoDeHistorialDto> Turnos)
{
    internal static ConversacionDetalleDto De(ConversacionDetalle c, bool veLaConsulta) =>
        new(c.Id, c.Titulo, c.CreadoEn, c.UltimaActividad,
            [.. c.Turnos.Select(t => TurnoDeHistorialDto.De(t, veLaConsulta))]);
}

/// <summary>Lo que el cliente manda para renombrar una conversación.</summary>
public sealed record RenombrarConversacionDto(
    [Required(AllowEmptyStrings = false)]
    [MaxLength(200)]
    string Titulo);

/// <summary>
/// Lo que el cliente de soporte manda en TODO pedido de historial ajeno.
/// </summary>
/// <remarks>
/// Va en el cuerpo del <c>POST</c> y nunca en la URL a propósito (design.md
/// D10 de asistente-historial-conversaciones): así la razón nunca termina en
/// un log de acceso, un proxy o el historial del navegador, la manera en que
/// terminaría si viajara por query string.
/// </remarks>
public sealed record RazonDto(string? Razon);

/// <summary>Lo que devuelve reanudar una conversación.</summary>
/// <param name="Hilo">
/// El id efímero NUEVO (mismo campo que <c>POST /consultas</c> ya devuelve),
/// para seguir preguntando en la misma conversación.
/// </param>
public sealed record ReanudarDto(Guid Hilo, IReadOnlyList<TurnoDeHistorialDto> Turnos);

/// <summary>Lo que devuelve «volver a consultar» un turno propio ya respondido.</summary>
/// <remarks>
/// Nunca un error HTTP crudo cuando la SQL ya no corre (design.md D4): eso
/// resuelve como <see cref="Exitosa"/> en falso con un <see cref="Mensaje"/>
/// no técnico, igual que el carril en vivo abstiene en vez de romper.
/// </remarks>
public sealed record ReejecucionDto
{
    public required bool Exitosa { get; init; }

    /// <summary>Presente solo cuando <see cref="Exitosa"/> es falso.</summary>
    public string? Mensaje { get; init; }

    public IReadOnlyList<ColumnaDto> Columnas { get; init; } = [];

    public IReadOnlyList<IReadOnlyList<object?>> Filas { get; init; } = [];

    public bool Truncado { get; init; }
}

/// <summary>Traduce <see cref="EstadoDelTurno"/> al nombre del contrato HTTP.</summary>
/// <remarks>
/// Duplicado deliberado de la lógica privada de <c>RespuestaDelAsistente</c>:
/// esa está atada a <c>ResultadoDelTurno</c> (que trae otros catorce campos
/// que el historial no tiene), y este helper sólo necesita el enum. Los
/// cuatro nombres son la misma promesa hacia el mismo cliente, así que un test
/// los compara contra los de <c>RespuestaDelAsistente</c>.
/// </remarks>
internal static class NombreDelEstado
{
    public static string De(EstadoDelTurno estado) => estado switch
    {
        EstadoDelTurno.Respondida => "respondida",
        EstadoDelTurno.NoContestable => "no_contestable",
        EstadoDelTurno.NecesitaAclaracion => "necesita_aclaracion",
        EstadoDelTurno.ServicioDegradado => "servicio_degradado",
        _ => throw new ArgumentOutOfRangeException(nameof(estado), estado, "Estado desconocido."),
    };
}
