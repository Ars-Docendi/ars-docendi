using System.Text.Json;
using System.Text.Json.Serialization;
using ArsDocendi.Evaluacion.Nucleo.Puntuacion;

namespace ArsDocendi.Evaluacion.Nucleo.Runner;

/// <summary>
/// El veredicto de cada ítem en la corrida que se tomó como referencia.
/// </summary>
/// <remarks>
/// Es un archivo <b>versionado</b>: su diff es la historia del comportamiento del
/// asistente, ítem por ítem.
///
/// Guarda el desenlace y no el puntaje: el puntaje depende de la penalización
/// elegida, que es una decisión de producto que todavía no está tomada, y una línea
/// de base atada a ella habría que regenerarla cada vez que se discuta el número.
/// </remarks>
public sealed record LineaDeBase(
    string Eje,
    SelloDeIdentidad Sello,
    IReadOnlyDictionary<string, DesenlaceDeItem> Items)
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    /// <summary>Deriva la línea de base de un reporte.</summary>
    /// <remarks>
    /// Es explícito y nunca un efecto de correr el eje: si fuera automático, una
    /// regresión real se absorbería sola en el primer commit que la causara, y el
    /// gate no detectaría nada nunca.
    /// </remarks>
    public static LineaDeBase De(Reporte reporte)
    {
        ArgumentNullException.ThrowIfNull(reporte);

        return new LineaDeBase(
            reporte.Eje,
            reporte.Sello,
            reporte.Resultados.ToDictionary(
                resultado => resultado.Id, resultado => resultado.Desenlace, StringComparer.Ordinal));
    }

    /// <summary>Serializa la línea de base.</summary>
    public string Serializar() => JsonSerializer.Serialize(this, Opciones);

    /// <summary>Interpreta una línea de base desde su texto.</summary>
    public static LineaDeBase Interpretar(string crudo) =>
        JsonSerializer.Deserialize<LineaDeBase>(crudo, Opciones)
        ?? throw new InvalidOperationException("La línea de base no se pudo interpretar.");

    /// <summary>Carga la línea de base de un archivo, o nulo si no existe todavía.</summary>
    public static LineaDeBase? Cargar(string ruta) =>
        File.Exists(ruta) ? Interpretar(File.ReadAllText(ruta)) : null;
}
