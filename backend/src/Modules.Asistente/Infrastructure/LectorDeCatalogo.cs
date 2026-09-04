using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>Una columna que la conexión actual puede leer.</summary>
internal sealed record ColumnaLegible(
    string Esquema,
    string Tabla,
    string Columna,
    string Tipo,
    bool Obligatoria,
    string? ComentarioDeTabla,
    string? ComentarioDeColumna);

/// <summary>Una clave foránea entre dos columnas legibles.</summary>
internal sealed record ReferenciaLegible(
    string Esquema,
    string Tabla,
    string Columna,
    string EsquemaReferido,
    string TablaReferida,
    string ColumnaReferida);

/// <summary>
/// Lee del catálogo de PostgreSQL lo que la conexión actual puede leer.
/// </summary>
/// <remarks>
/// La pregunta que le hace a la base es literalmente «¿qué puedo leer yo?»:
/// <c>has_column_privilege</c> y <c>has_schema_privilege</c> se evalúan contra
/// <c>current_user</c>. Por eso el resultado cambia solo cuando cambian los
/// <c>GRANT</c>, y por eso los dos roles del asistente obtienen esquemas
/// distintos sin que este código sepa nada de ellos.
///
/// Todo va calificado con <c>pg_catalog.</c> porque los roles del asistente
/// corren con <c>search_path = ''</c>.
/// </remarks>
internal static class LectorDeCatalogo
{
    /// <summary>
    /// Esquemas del sistema, que nunca forman parte del esquema del asistente.
    /// </summary>
    /// <remarks>
    /// No hace falta excluir <c>audit</c>: el manifiesto le revoca <c>USAGE</c>,
    /// así que <c>has_schema_privilege</c> ya lo deja afuera. Excluirlo acá por
    /// las dudas escondería una regresión de ese <c>REVOKE</c>.
    /// </remarks>
    private const string EsquemasDelSistema =
        "('pg_catalog', 'information_schema', 'pg_toast')";

    private const string SqlColumnas = $"""
        SELECT n.nspname                                        AS esquema,
               c.relname                                        AS tabla,
               a.attname                                        AS columna,
               pg_catalog.format_type(a.atttypid, a.atttypmod)   AS tipo,
               a.attnotnull                                     AS obligatoria,
               pg_catalog.obj_description(c.oid, 'pg_class')     AS comentario_tabla,
               pg_catalog.col_description(c.oid, a.attnum)       AS comentario_columna
          FROM pg_catalog.pg_class c
          JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
          JOIN pg_catalog.pg_attribute a ON a.attrelid = c.oid
         WHERE c.relkind = 'r'
           AND a.attnum > 0
           AND NOT a.attisdropped
           AND n.nspname NOT IN {EsquemasDelSistema}
           AND pg_catalog.has_schema_privilege(c.relnamespace, 'USAGE')
           AND pg_catalog.has_column_privilege(c.oid, a.attnum, 'SELECT')
         ORDER BY n.nspname, c.relname, a.attnum
        """;

    // Solo las referencias cuyos DOS extremos son legibles: describirle al modelo
    // un join hacia una tabla que no puede leer lo empuja a escribirlo y a chocar
    // con permission denied, que es peor que no conocer el camino.
    private const string SqlReferencias = $"""
        SELECT n.nspname   AS esquema,
               c.relname   AS tabla,
               a.attname   AS columna,
               fn.nspname  AS esquema_referido,
               fc.relname  AS tabla_referida,
               fa.attname  AS columna_referida
          FROM pg_catalog.pg_constraint con
          JOIN pg_catalog.pg_class      c  ON c.oid  = con.conrelid
          JOIN pg_catalog.pg_namespace  n  ON n.oid  = c.relnamespace
          JOIN pg_catalog.pg_class      fc ON fc.oid = con.confrelid
          JOIN pg_catalog.pg_namespace  fn ON fn.oid = fc.relnamespace
          -- generate_subscripts y no unnest de dos argumentos: la forma multiargumento
          -- de unnest es una construcción del parser que solo vale suelta en el FROM,
          -- así que calificada con pg_catalog no resuelve — y calificar es obligatorio
          -- porque estos roles corren con search_path vacío.
          CROSS JOIN LATERAL pg_catalog.generate_subscripts(con.conkey, 1) AS posicion
          JOIN pg_catalog.pg_attribute  a  ON a.attrelid  = c.oid  AND a.attnum  = con.conkey[posicion]
          JOIN pg_catalog.pg_attribute  fa ON fa.attrelid = fc.oid AND fa.attnum = con.confkey[posicion]
         WHERE con.contype = 'f'
           AND n.nspname  NOT IN {EsquemasDelSistema}
           AND fn.nspname NOT IN {EsquemasDelSistema}
           AND pg_catalog.has_schema_privilege(c.relnamespace,  'USAGE')
           AND pg_catalog.has_schema_privilege(fc.relnamespace, 'USAGE')
           AND pg_catalog.has_column_privilege(c.oid,  a.attnum,  'SELECT')
           AND pg_catalog.has_column_privilege(fc.oid, fa.attnum, 'SELECT')
         ORDER BY n.nspname, c.relname, a.attname
        """;

    public static async Task<IReadOnlyList<ColumnaLegible>> LeerColumnasAsync(
        NpgsqlConnection conexion, CancellationToken ct)
    {
        var columnas = new List<ColumnaLegible>();

        await using var comando = new NpgsqlCommand(SqlColumnas, conexion);
        await using var lector = await comando.ExecuteReaderAsync(ct);

        while (await lector.ReadAsync(ct))
        {
            columnas.Add(new ColumnaLegible(
                lector.GetString(0),
                lector.GetString(1),
                lector.GetString(2),
                lector.GetString(3),
                lector.GetBoolean(4),
                lector.IsDBNull(5) ? null : lector.GetString(5),
                lector.IsDBNull(6) ? null : lector.GetString(6)));
        }

        return columnas;
    }

    public static async Task<IReadOnlyList<ReferenciaLegible>> LeerReferenciasAsync(
        NpgsqlConnection conexion, CancellationToken ct)
    {
        var referencias = new List<ReferenciaLegible>();

        await using var comando = new NpgsqlCommand(SqlReferencias, conexion);
        await using var lector = await comando.ExecuteReaderAsync(ct);

        while (await lector.ReadAsync(ct))
        {
            referencias.Add(new ReferenciaLegible(
                lector.GetString(0),
                lector.GetString(1),
                lector.GetString(2),
                lector.GetString(3),
                lector.GetString(4),
                lector.GetString(5)));
        }

        return referencias;
    }
}
