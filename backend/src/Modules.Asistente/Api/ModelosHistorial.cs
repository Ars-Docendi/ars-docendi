using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Modules.Asistente.Application;

namespace Modules.Asistente.Api;

/// <summary>Una conversación propia, en la lista.</summary>
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
/// Una mención de un turno histórico, re-resuelta para el actor QUE LEE ahora — nunca para el que
/// hizo la pregunta originalmente (design.md D11 de asistente-rediseno-v3, decisión 15 del PO,
/// 2026-09-26).
/// </summary>
public sealed record MencionDeHistorialDto(string Tipo, Guid Id, string Etiqueta);

/// <summary>Un turno de una conversación propia.</summary>
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

    /// <summary>
    /// Todos los turnos de una conversación, en orden y de a uno: cada mención
    /// resuelta abre una conexión de lectura, y en paralelo una conversación larga
    /// tomaría del pool tantas como menciones tenga.
    /// </summary>
    internal static async Task<IReadOnlyList<TurnoDeHistorialDto>> DeTodosAsync(
        IReadOnlyList<TurnoDeHistorial> turnos, bool veLaConsulta, Guid actorQueLee,
        IBuscadorDeMenciones menciones, CancellationToken ct)
    {
        var resultado = new List<TurnoDeHistorialDto>(turnos.Count);
        foreach (var turno in turnos)
        {
            resultado.Add(await DeAsync(turno, veLaConsulta, actorQueLee, menciones, ct));
        }

        return resultado;
    }

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
        var turnos = await TurnoDeHistorialDto.DeTodosAsync(c.Turnos, veLaConsulta, actorQueLee, menciones, ct);

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
public sealed record ReanudarDto(Guid Hilo, IReadOnlyList<TurnoDeHistorialDto> Turnos);

/// <summary>Lo que devuelve «volver a consultar» un turno propio ya respondido.</summary>
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
