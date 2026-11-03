using System.Globalization;
using System.Text;

namespace Modules.Asistente.Application;

/// <summary>
/// Pone el catálogo de capacidades en español, para la meta-pregunta. No nombra tablas ni schemas:
/// son etiquetas internas (RNF-18).
/// </summary>
internal static class RedaccionDeCapacidades
{
    /// <summary>Cuántas áreas se nombran antes de resumir el resto.</summary>
    private const int AreasQueSeNombran = 6;

    /// <summary>Redacta el catálogo.</summary>
    public static string Texto(CapacidadesDelActor capacidades)
    {
        ArgumentNullException.ThrowIfNull(capacidades);

        var texto = new StringBuilder();

        // La misma presentación que la pantalla inicial, y no una redacción propia:
        // las dos superficies contestan la misma pregunta —«¿qué podés hacer?»— y
        // que se contradijeran sería el defecto más visible de las dos.
        texto.AppendLine(capacidades.Presentacion);
        texto.AppendLine();

        texto.Append(CultureInfo.InvariantCulture, $"Puedo consultar {Cuantas(capacidades)}. ");
        texto.Append(capacidades.Alcance);

        var descritas = capacidades.Cubre
            .Where(area => !string.IsNullOrWhiteSpace(area.Descripcion))
            .OrderByDescending(area => area.Columnas)
            .ThenBy(area => area.Nombre, StringComparer.Ordinal)
            .Take(AreasQueSeNombran)
            .ToList();

        if (descritas.Count > 0)
        {
            texto.AppendLine();
            texto.AppendLine();
            texto.AppendLine("Sobre qué:");

            foreach (var area in descritas)
            {
                texto.AppendLine(CultureInfo.InvariantCulture, $"- {Primera(area.Descripcion!)}");
            }
        }

        if (capacidades.Ejemplos.Count > 0)
        {
            texto.AppendLine();
            texto.AppendLine("Por ejemplo:");

            foreach (var ejemplo in capacidades.Ejemplos)
            {
                texto.AppendLine(CultureInfo.InvariantCulture, $"- {ejemplo}");
            }
        }

        texto.AppendLine();
        texto.AppendLine("Lo que no hago:");

        foreach (var limite in capacidades.NoPuede)
        {
            texto.AppendLine(CultureInfo.InvariantCulture, $"- {limite}");
        }

        return texto.ToString().TrimEnd();
    }

    private static string Cuantas(CapacidadesDelActor capacidades) =>
        capacidades.Tablas == 1
            ? "un área de datos del sistema"
            : $"{capacidades.Tablas} áreas de datos del sistema";

    /// <summary>La primera oración del comentario de la tabla.</summary>
    private static string Primera(string comentario)
    {
        var punto = comentario.IndexOf('.', StringComparison.Ordinal);

        return punto > 0 ? comentario[..(punto + 1)] : comentario;
    }
}
