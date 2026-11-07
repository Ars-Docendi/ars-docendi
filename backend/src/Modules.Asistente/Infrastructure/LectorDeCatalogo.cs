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

/// <summary>Lee del catálogo de PostgreSQL lo que la conexión actual puede leer.</summary>
internal static class LectorDeCatalogo
{
    /// <summary>Esquemas del sistema, que nunca forman parte del esquema del asistente.</summary>
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

        UNION

        -- Referencias LÓGICAS entre módulos: sin FK a propósito (la integridad la
        -- sostienen los triggers de identity.proteger_borrado_destino_logico y la
        -- validación al escribir), pero son el join de toda pregunta útil sobre el
        -- portal. Pasan por los mismos chequeos de privilegio que las FK.
        SELECT v.esquema, v.tabla, v.columna,
               v.esquema_referido, v.tabla_referida, v.columna_referida
          FROM (VALUES {ReferenciasLogicas}) AS v(esquema, tabla, columna,
                                                  esquema_referido, tabla_referida, columna_referida)
         WHERE pg_catalog.has_schema_privilege(v.esquema, 'USAGE')
           AND pg_catalog.has_schema_privilege(v.esquema_referido, 'USAGE')
           AND pg_catalog.has_column_privilege(
                   pg_catalog.quote_ident(v.esquema) || '.' || pg_catalog.quote_ident(v.tabla),
                   v.columna, 'SELECT')
           AND pg_catalog.has_column_privilege(
                   pg_catalog.quote_ident(v.esquema_referido) || '.' || pg_catalog.quote_ident(v.tabla_referida),
                   v.columna_referida, 'SELECT')

         ORDER BY 1, 2, 3
        """;

    /// <summary>Referencias entre schemas que el modelo de datos mantiene sin FK.</summary>
    private const string ReferenciasLogicas =
        "('portal', 'perfiles', 'persona_id', 'identity', 'personas', 'id')";

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
