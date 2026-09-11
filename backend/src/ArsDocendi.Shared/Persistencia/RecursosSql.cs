using System.Reflection;

namespace ArsDocendi.Shared.Persistencia;

/// <summary>
/// Lee el DDL versionado que se embebe como recurso del assembly.
/// <para>
/// Los archivos <c>.sql</c> bajo <c>database/</c> son la fuente autorizada del schema:
/// contienen construcciones que EF Core no sabe generar (funciones y triggers plpgsql,
/// <c>NULLS NOT DISTINCT</c>, constraints <c>EXCLUDE</c>, las llamadas a
/// <c>audit.attach</c>). Las migraciones los ejecutan con <c>migrationBuilder.Sql(...)</c>.
/// </para>
/// <para>
/// Se resuelve por sufijo del nombre lógico del recurso —y no por nombre completo— para
/// no acoplar el código a cómo MSBuild derive ese nombre del <c>Link</c> del csproj.
/// </para>
/// </summary>
public static class RecursosSql
{
    /// <summary>
    /// Devuelve el contenido del recurso embebido cuyo nombre lógico termina en
    /// <paramref name="rutaRelativa"/> (por ejemplo <c>"identity/006_identity_personas.sql"</c>).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Si no hay exactamente un recurso que coincida. Fallar acá es deliberado: un DDL que
    /// no se encuentra en tiempo de migración debe romper el arranque, no aplicarse a medias.
    /// </exception>
    public static string Leer(Assembly assembly, string rutaRelativa)
    {
        var sufijo = rutaRelativa.Replace('/', '.').Replace('\\', '.');

        var coincidencias = assembly
            .GetManifestResourceNames()
            .Where(nombre => nombre.EndsWith(sufijo, StringComparison.Ordinal))
            .ToArray();

        if (coincidencias.Length != 1)
        {
            throw new InvalidOperationException(
                $"Se esperaba exactamente un recurso embebido terminado en '{sufijo}' dentro de " +
                $"'{assembly.GetName().Name}', pero se encontraron {coincidencias.Length}. " +
                $"Recursos disponibles: {string.Join(", ", assembly.GetManifestResourceNames())}");
        }

        using var stream = assembly.GetManifestResourceStream(coincidencias[0])
            ?? throw new InvalidOperationException($"No se pudo abrir el recurso '{coincidencias[0]}'.");

        using var lector = new StreamReader(stream);
        return lector.ReadToEnd();
    }
}
