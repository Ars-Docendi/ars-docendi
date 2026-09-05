using System.ComponentModel.DataAnnotations;
using Modules.Asistente.Application;

namespace Modules.Asistente.Api;

/// <summary>Lo que el cliente manda en un turno.</summary>
/// <param name="Mensaje">Lo que escribió el usuario.</param>
/// <param name="Hilo">
/// El hilo del turno anterior, para que el seguimiento funcione. Nulo en el primero.
/// </param>
/// <remarks>
/// <b>No trae al actor.</b> El actor sale de la identidad de la sesión y de ningún
/// otro lado: un identificador tomado del cuerpo del pedido sería un selector de
/// alcance controlado por el cliente.
/// </remarks>
public sealed record ConsultaDelAsistente(
    // Las anotaciones van en el PARÁMETRO y no en la propiedad. Con
    // `[property: ...]` sobre un parámetro del constructor primario, ASP.NET Core
    // levanta excepción al validar el modelo —no las ignora en silencio— y todo
    // request al endpoint termina en 500.
    [Required(AllowEmptyStrings = false)]
    [MaxLength(2000)]
    string Mensaje,
    Guid? Hilo);

/// <summary>Una opción del menú de aclaración.</summary>
public sealed record OpcionDto(string Etiqueta, string PreguntaResuelta);

/// <summary>Una columna del resultado, con su marca de sensibilidad.</summary>
/// <param name="Sensible">
/// Si la columna trae un dato personal. Lo necesita quien renderiza: con columnas
/// sensibles la narración deja de ser el vehículo del dato —el modelo redacta el
/// marco y la interfaz muestra la tabla—.
/// </param>
public sealed record ColumnaDto(string Nombre, bool Sensible);

/// <summary>Lo que el cliente recibe de un turno (§4.6).</summary>
public sealed record RespuestaDelAsistente
{
    /// <summary>Uno de los cuatro: respondida, no contestable, necesita aclaración, degradado.</summary>
    public required string Estado { get; init; }

    /// <summary>El texto que lee el usuario.</summary>
    public required string Respuesta { get; init; }

    /// <summary>El hilo, para mandarlo en el turno siguiente.</summary>
    public required Guid Hilo { get; init; }

    /// <summary>Cómo se interpretó la pregunta. Presente solo si difiere del mensaje.</summary>
    public string? PreguntaInterpretada { get; init; }

    /// <summary>Cómo el asistente llegó a la consulta, tal como lo devolvió la generación.</summary>
    public string? Razonamiento { get; init; }

    /// <summary>Las opciones de una aclaración. <b>Bloquean</b> el turno.</summary>
    public IReadOnlyList<OpcionDto> Opciones { get; init; } = [];

    /// <summary>Qué otra cosa probar. <b>No</b> bloquean nada.</summary>
    public IReadOnlyList<string> Sugerencias { get; init; } = [];

    /// <summary>Las columnas del resultado, con su marca de sensibilidad.</summary>
    public IReadOnlyList<ColumnaDto> Columnas { get; init; } = [];

    /// <summary>Las filas, con los valores reales.</summary>
    public IReadOnlyList<IReadOnlyList<object?>> Filas { get; init; } = [];

    /// <summary>
    /// Si hubo más filas que el tope. Booleano y nunca un conteo: cuántas quedaron
    /// afuera es un canal de inferencia sobre datos que el usuario no puede ver.
    /// </summary>
    public bool Truncado { get; init; }

    /// <summary>La consulta que se ejecutó. Presente solo con el permiso correspondiente.</summary>
    public string? Sql { get; init; }

    /// <summary>Lo que costó el turno.</summary>
    public required MetricasDto Metricas { get; init; }

    /// <summary>Arma la respuesta HTTP a partir del resultado del turno.</summary>
    public static RespuestaDelAsistente De(ResultadoDelTurno turno)
    {
        ArgumentNullException.ThrowIfNull(turno);

        return new RespuestaDelAsistente
        {
            Estado = Nombrar(turno.Estado),
            Respuesta = turno.Respuesta,
            Hilo = turno.Hilo,
            PreguntaInterpretada = turno.PreguntaInterpretada,
            Razonamiento = string.IsNullOrWhiteSpace(turno.Razonamiento) ? null : turno.Razonamiento,
            Opciones = [.. (turno.Opciones ?? []).Select(o => new OpcionDto(o.Etiqueta, o.PreguntaResuelta))],
            Sugerencias = turno.Sugerencias ?? [],
            Columnas = [.. turno.Columnas.Select((nombre, i) =>
                new ColumnaDto(nombre, turno.Sensibilidad.Count > i && turno.Sensibilidad[i].Tapa))],
            Filas = turno.Filas,
            Truncado = turno.Truncado,
            Sql = turno.Sql,
            Metricas = new MetricasDto(turno.LlamadasAlModelo, turno.Categoria),
        };
    }

    /// <summary>
    /// Traduce el estado a la forma del contrato HTTP.
    /// </summary>
    /// <remarks>
    /// Explícito y no <c>ToString()</c>: el nombre del enum es un detalle interno del
    /// backend, y renombrarlo no puede romper a los clientes en silencio.
    /// </remarks>
    private static string Nombrar(EstadoDelTurno estado) => estado switch
    {
        EstadoDelTurno.Respondida => "respondida",
        EstadoDelTurno.NoContestable => "no_contestable",
        EstadoDelTurno.NecesitaAclaracion => "necesita_aclaracion",
        EstadoDelTurno.ServicioDegradado => "servicio_degradado",
        _ => throw new ArgumentOutOfRangeException(nameof(estado), estado, "Estado desconocido."),
    };
}

/// <summary>Lo que costó el turno.</summary>
public sealed record MetricasDto(int LlamadasAlModelo, string Categoria);

/// <summary>Un área que el actor puede consultar.</summary>
public sealed record AreaDto(string Nombre, string? Descripcion, int Columnas);

/// <summary>El catálogo de capacidades del actor.</summary>
public sealed record CapacidadesDto
{
    /// <summary>Las áreas, con sus conteos.</summary>
    public required IReadOnlyList<AreaDto> Cubre { get; init; }

    /// <summary>Cuántas tablas puede consultar.</summary>
    public required int Tablas { get; init; }

    /// <summary>Cuántas columnas puede leer.</summary>
    public required int Columnas { get; init; }

    /// <summary>Preguntas ejecutables, verificadas contra sus privilegios.</summary>
    public required IReadOnlyList<string> Ejemplos { get; init; }

    /// <summary>Los límites del asistente.</summary>
    public required IReadOnlyList<string> NoPuede { get; init; }

    /// <summary>Qué filas ve, dicho aparte de los conteos.</summary>
    public required string Alcance { get; init; }

    /// <summary>Por qué cosas suele venir a preguntar este actor, según su rol.</summary>
    public required string Presentacion { get; init; }

    /// <summary>Arma el DTO a partir del catálogo.</summary>
    public static CapacidadesDto De(CapacidadesDelActor capacidades)
    {
        ArgumentNullException.ThrowIfNull(capacidades);

        return new CapacidadesDto
        {
            Cubre = [.. capacidades.Cubre.Select(a => new AreaDto(a.Nombre, a.Descripcion, a.Columnas))],
            Tablas = capacidades.Tablas,
            Columnas = capacidades.Columnas,
            Ejemplos = capacidades.Ejemplos,
            NoPuede = capacidades.NoPuede,
            Alcance = capacidades.Alcance,
            Presentacion = capacidades.Presentacion,
        };
    }
}
