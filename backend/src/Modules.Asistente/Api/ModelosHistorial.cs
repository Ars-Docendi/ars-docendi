using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
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

/// <summary>
/// Una mención de un turno histórico, re-resuelta para el actor QUE LEE
/// ahora — nunca para el que hizo la pregunta originalmente (design.md D11
/// de asistente-rediseno-v3, decisión 15 del PO, 2026-09-26).
/// </summary>
/// <param name="Tipo">
/// <c>"materia"</c> o <c>"docente"</c>, igual que <see cref="ReferenciaDto.Tipo"/>.
/// </param>
/// <param name="Etiqueta">
/// El texto exacto que el composer insertó al elegirla —«@Nombre» o
/// «#Nombre», el mismo formato que <c>textoDeLaMencion</c> del frontend—,
/// para que <c>ubicarMenciones</c> (ya usado por «Editar y reenviar») ubique
/// el chip en el texto de la pregunta sin que este lado calcule posiciones.
/// Si el nombre cambió desde entonces el texto ya no calza y la mención
/// vuelve a texto plano sola, el mismo comportamiento que borrar el texto de
/// una mención antes de enviar.
/// </param>
public sealed record MencionDeHistorialDto(string Tipo, Guid Id, string Etiqueta);

/// <summary>Un turno de una conversación propia.</summary>
/// <param name="Sql">
/// Presente solo con <c>asistente.ver_consulta</c> (design.md D9 de
/// asistente-historial-conversaciones) — mismo criterio que el campo
/// homónimo del turno en vivo.
/// </param>
/// <param name="Menciones">
/// <c>null</c> —y por lo tanto AUSENTE del JSON, ver el atributo— cuando
/// nadie las resolvió: es lo que <see cref="De"/> siembra para
/// <see cref="SoporteHistorialController"/>, que nunca es el actor cuyo
/// alcance decide la visibilidad de una mención ajena. <see cref="DeAsync"/>
/// —el lado propio, <see cref="HistorialController"/>— siempre manda una
/// lista, aunque quede vacía porque ninguna referencia sobrevivió la
/// revalidación (decisión 15 del PO, design.md D11).
/// </param>
public sealed record TurnoDeHistorialDto(
    Guid Id,
    string Pregunta,
    string? Sql,
    string Estado,
    DateTimeOffset OcurrioEn,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<MencionDeHistorialDto>? Menciones = null)
{
    /// <summary>La lectura de soporte: nunca resuelve menciones (decisión 15 del PO).</summary>
    internal static TurnoDeHistorialDto De(TurnoDeHistorial t, bool veLaConsulta) =>
        new(t.Id, t.Pregunta, veLaConsulta ? t.SqlResuelto : null, NombreDelEstado.De(t.Estado), t.OcurrioEn);

    /// <summary>El lado propio: re-resuelve cada referencia para <paramref name="actorQueLee"/>.</summary>
    internal static async Task<TurnoDeHistorialDto> DeAsync(
        TurnoDeHistorial t, bool veLaConsulta, Guid actorQueLee, IBuscadorDeMenciones menciones,
        CancellationToken ct) =>
        new(t.Id, t.Pregunta, veLaConsulta ? t.SqlResuelto : null, NombreDelEstado.De(t.Estado), t.OcurrioEn,
            await ResolverMencionesAsync(t.Referencias, actorQueLee, menciones, ct));

    private static async Task<IReadOnlyList<MencionDeHistorialDto>> ResolverMencionesAsync(
        IReadOnlyDictionary<string, (TipoDeMencion Tipo, Guid Id)>? referencias,
        Guid actor, IBuscadorDeMenciones menciones, CancellationToken ct)
    {
        if (referencias is null or { Count: 0 })
        {
            return [];
        }

        var resueltas = new List<MencionDeHistorialDto>(referencias.Count);
        foreach (var referencia in referencias.Values)
        {
            var resuelta = await menciones.ResolverAsync(actor, referencia.Tipo, referencia.Id, ct);
            if (resuelta is null)
            {
                // Fuera del alcance ACTUAL de quien lee: se omite, nunca se filtra
                // (decisión 15 del PO) — el mismo criterio sin oráculo de existencia
                // que ya usa la revalidación de «Volver a consultar» (design.md D11).
                continue;
            }

            var disparador = referencia.Tipo == TipoDeMencion.Materia ? "@" : "#";
            resueltas.Add(new MencionDeHistorialDto(
                NombreDelTipoDeMencion.De(referencia.Tipo), referencia.Id, $"{disparador}{resuelta.Nombre}"));
        }

        return resueltas;
    }
}

/// <summary>Traduce <see cref="TipoDeMencion"/> al nombre del contrato HTTP.</summary>
/// <remarks>
/// Duplicado deliberado de <c>AsistenteController.TipoDeMencionDe</c> (que va en la
/// dirección opuesta, string → enum, y es privado a ese controller): este lado
/// sólo necesita enum → string, para <see cref="MencionDeHistorialDto"/>.
/// </remarks>
internal static class NombreDelTipoDeMencion
{
    public static string De(TipoDeMencion tipo) => tipo switch
    {
        TipoDeMencion.Materia => "materia",
        TipoDeMencion.Docente => "docente",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de mención desconocido."),
    };
}

/// <summary>Una conversación propia, con sus turnos.</summary>
public sealed record ConversacionDetalleDto(
    Guid Id,
    string Titulo,
    DateTimeOffset CreadoEn,
    DateTimeOffset UltimaActividad,
    IReadOnlyList<TurnoDeHistorialDto> Turnos)
{
    /// <summary>La lectura de soporte: nunca resuelve menciones (decisión 15 del PO).</summary>
    internal static ConversacionDetalleDto De(ConversacionDetalle c, bool veLaConsulta) =>
        new(c.Id, c.Titulo, c.CreadoEn, c.UltimaActividad,
            [.. c.Turnos.Select(t => TurnoDeHistorialDto.De(t, veLaConsulta))]);

    /// <summary>El lado propio: re-resuelve las menciones de cada turno para <paramref name="actorQueLee"/>.</summary>
    internal static async Task<ConversacionDetalleDto> DeAsync(
        ConversacionDetalle c, bool veLaConsulta, Guid actorQueLee, IBuscadorDeMenciones menciones,
        CancellationToken ct)
    {
        var turnos = await Task.WhenAll(
            c.Turnos.Select(t => TurnoDeHistorialDto.DeAsync(t, veLaConsulta, actorQueLee, menciones, ct)));

        return new(c.Id, c.Titulo, c.CreadoEn, c.UltimaActividad, turnos);
    }
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
