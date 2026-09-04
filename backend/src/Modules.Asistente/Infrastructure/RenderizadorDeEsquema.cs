using System.Globalization;
using System.Text;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Convierte lo leído del catálogo en el texto del prompt de sistema.
/// </summary>
/// <remarks>
/// El orden de todo lo que emite es determinista —esquema, tabla y número de
/// columna— porque el prefijo tiene que ser idéntico byte a byte entre procesos.
/// Un orden que dependiera del recorrido de un diccionario haría que la huella
/// cambiara sin que cambiara nada, y cada arranque pagaría escritura de caché en
/// lugar de lectura.
/// </remarks>
internal static class RenderizadorDeEsquema
{
    /// <summary>
    /// Instrucciones fijas del carril SQL. Son parte del prefijo: no cambian por
    /// turno y por eso se cachean junto con el esquema.
    /// </summary>
    /// <remarks>
    /// La prohibición del reloj está acá <b>y</b> en el validador. Acá es un
    /// pedido; allá es una imposición. La duplicación es deliberada: pedirlo
    /// mejora la tasa de acierto y ahorra rechazos, imponerlo es lo que hace que
    /// la garantía valga.
    /// </remarks>
    private const string Instrucciones = """
        Sos el traductor de preguntas a SQL de un sistema de gestión docente
        universitaria. Recibís una pregunta en español y devolvés una consulta
        PostgreSQL que la responde, o declarás que no se puede responder.

        REGLAS QUE NO SE NEGOCIAN

        1. Solo `SELECT`. Nunca `INSERT`, `UPDATE`, `DELETE`, `CREATE`, `ALTER`,
           `DROP`, `GRANT` ni ninguna otra sentencia que modifique algo.
        2. Nunca uses funciones de reloj: ni `now()`, ni `current_date`, ni
           `current_timestamp`, ni ninguna de su familia. Cuando la pregunta
           hable del presente, usá la fecha de referencia que viene en el
           mensaje, o mejor todavía las banderas del dominio: el período con
           `activo = true`, la designación con `vigente_hasta IS NULL`.
        3. Nunca llames a `set_config`, `current_setting` ni a ninguna función de
           configuración de sesión, ni siquiera entre comillas dobles.
        4. Una sola sentencia. Sin punto y coma intermedios.
        5. Usá exclusivamente las tablas y columnas que aparecen más abajo. Si la
           pregunta necesita algo que no está listado, la respuesta es que no se
           puede contestar. No inventes tablas, columnas ni valores.
        6. No agregues `LIMIT`: el sistema envuelve tu consulta y le pone el suyo.
        7. Calificá siempre las tablas con su esquema: `identity.personas`, no
           `personas`.

        SOBRE EL ALCANCE

        La consulta se ejecuta con los permisos del usuario que pregunta, y la
        base filtra sola las filas que ese usuario no puede ver. No escribas
        ningún filtro de permisos ni de alcance: no hace falta y sería incorrecto.

        QUÉ DEVOLVER

        Devolvé un único objeto JSON, sin texto alrededor, con estas claves:

        - `es_contestable`: `true` si la pregunta se puede responder con las
          tablas listadas; `false` si no.
        - `sql`: la consulta, o `null` si no es contestable.
        - `razonamiento`: una o dos oraciones en español explicando cómo
          interpretaste la pregunta. Lo lee el usuario final, así que no
          menciones nombres de tablas ni de columnas.
        - `categoria`: una de `consulta_simple`, `filtro_temporal`,
          `cruce_de_tablas`, `agregacion`, `no_contestable`, `ambigua`.

        Si la pregunta nombra una materia sin decir de qué carrera, o a una
        persona solo por su apellido, y eso puede corresponder a más de una fila,
        marcá la categoría `ambigua` y explicá en el razonamiento qué falta.
        """;

    private const string EncabezadoDelEsquema = """

        ESQUEMA DISPONIBLE

        Estas son todas las tablas y columnas que podés consultar. Cualquier otra
        no existe para vos.
        """;

    public static string Renderizar(
        IReadOnlyList<ColumnaLegible> columnas,
        IReadOnlyList<ReferenciaLegible> referencias)
    {
        var texto = new StringBuilder(Instrucciones);
        texto.Append('\n');
        texto.Append(EncabezadoDelEsquema);
        texto.Append('\n');

        var porTabla = columnas
            .GroupBy(c => (c.Esquema, c.Tabla))
            .OrderBy(g => g.Key.Esquema, StringComparer.Ordinal)
            .ThenBy(g => g.Key.Tabla, StringComparer.Ordinal);

        foreach (var tabla in porTabla)
        {
            EscribirTabla(texto, tabla.Key.Esquema, tabla.Key.Tabla, [.. tabla]);
        }

        EscribirReferencias(texto, referencias);

        return texto.ToString();
    }

    private static void EscribirTabla(
        StringBuilder texto, string esquema, string tabla, IReadOnlyList<ColumnaLegible> columnas)
    {
        texto.Append(CultureInfo.InvariantCulture, $"\n## {esquema}.{tabla}\n");

        var descripcion = columnas[0].ComentarioDeTabla;
        if (!string.IsNullOrWhiteSpace(descripcion))
        {
            texto.Append(descripcion.Trim()).Append('\n');
        }

        foreach (var columna in columnas)
        {
            texto.Append(CultureInfo.InvariantCulture, $"- {columna.Columna} ({columna.Tipo}");
            if (!columna.Obligatoria)
            {
                texto.Append(", admite nulo");
            }

            texto.Append(')');

            if (!string.IsNullOrWhiteSpace(columna.ComentarioDeColumna))
            {
                texto.Append(": ").Append(columna.ComentarioDeColumna.Trim());
            }

            texto.Append('\n');
        }
    }

    private static void EscribirReferencias(
        StringBuilder texto, IReadOnlyList<ReferenciaLegible> referencias)
    {
        if (referencias.Count == 0)
        {
            return;
        }

        texto.Append("\n## Cómo se relacionan\n");

        var ordenadas = referencias
            .OrderBy(r => r.Esquema, StringComparer.Ordinal)
            .ThenBy(r => r.Tabla, StringComparer.Ordinal)
            .ThenBy(r => r.Columna, StringComparer.Ordinal);

        foreach (var referencia in ordenadas)
        {
            texto.Append(CultureInfo.InvariantCulture,
                $"- {referencia.Esquema}.{referencia.Tabla}.{referencia.Columna}"
                + $" referencia a {referencia.EsquemaReferido}.{referencia.TablaReferida}"
                + $".{referencia.ColumnaReferida}\n");
        }
    }
}
