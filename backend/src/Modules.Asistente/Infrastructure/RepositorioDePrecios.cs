using ArsDocendi.Shared.Persistencia;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>One version of a model price, stored PER TOKEN as <c>tabla_de_precios</c> does.</summary>
internal sealed record VersionDePrecio(
    string Empresa,
    string Modelo,
    decimal EntradaPorToken,
    decimal SalidaPorToken,
    decimal CachePorToken,
    int Version,
    DateTimeOffset VigenteDesde,
    DateTimeOffset? VigenteHasta);

/// <summary>How a price write ended.</summary>
internal enum DesenlaceDeEscrituraDePrecio
{
    Escrito,

    /// <summary>The effective moment is earlier than the start of the version in force; nothing changed.</summary>
    VigenciaAnteriorALaVersionActual,
}

/// <summary>The outcome of <see cref="RepositorioDePrecios.EscribirNuevaVersionAsync"/>.</summary>
internal sealed record EscrituraDePrecio(
    DesenlaceDeEscrituraDePrecio Desenlace, VersionDePrecio? Anterior, VersionDePrecio? Nueva);

/// <summary>
/// The writer of <c>asistente.tabla_de_precios</c> (asistente-modelo-en-caliente, D9):
/// every write closes the open version and inserts a new one in ONE transaction. A
/// stored row is never edited in place beyond closing it.
/// </summary>
/// <remarks>
/// Readers (<see cref="ConsultasDeUso"/>, <see cref="PresupuestoOrganizacionalPersistente"/>,
/// <c>CalculadoraDeCosto</c>) pick the row whose half-open range
/// <c>[vigente_desde, vigente_hasta)</c> contains the moment. Closing the previous row
/// AT the new row's start keeps the ranges contiguous and non-overlapping, so exactly
/// one row matches any moment. Owner connection: the table sits inside the
/// wholesale-revoked <c>asistente</c> schema.
/// </remarks>
internal sealed class RepositorioDePrecios(CadenaDuena cadena)
{
    private const string Columnas =
        "proveedor, modelo, precio_por_token_entrada, precio_por_token_salida, precio_por_token_cache, version, vigente_desde, vigente_hasta";

    /// <summary>The version in force at <paramref name="momento"/> for every priced model.</summary>
    public async Task<IReadOnlyList<VersionDePrecio>> ListarVigentesAsync(DateTimeOffset momento, CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        await using var comando = new NpgsqlCommand(
            $"""
            SELECT DISTINCT ON (proveedor, modelo) {Columnas}
              FROM asistente.tabla_de_precios
             WHERE vigente_desde <= @momento
               AND (vigente_hasta IS NULL OR @momento < vigente_hasta)
             ORDER BY proveedor, modelo, vigente_desde DESC
            """, conexion);
        comando.Parameters.AddWithValue("momento", momento);

        var versiones = new List<VersionDePrecio>();
        await using var lector = await comando.ExecuteReaderAsync(ct);
        while (await lector.ReadAsync(ct))
        {
            versiones.Add(Leer(lector));
        }

        return versiones;
    }

    /// <summary>
    /// Closes the open version of <paramref name="empresa"/>/<paramref name="modelo"/> at
    /// <paramref name="vigenteDesde"/> and opens the new one, atomically. Rejects (changing
    /// nothing) a start earlier than the open version's. Equal is allowed: the old row
    /// becomes an empty range that no moment matches.
    /// </summary>
    public async Task<EscrituraDePrecio> EscribirNuevaVersionAsync(
        string empresa,
        string modelo,
        decimal entradaPorToken,
        decimal salidaPorToken,
        decimal cachePorToken,
        DateTimeOffset vigenteDesde,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(empresa);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelo);

        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);
        await using var transaccion = await conexion.BeginTransactionAsync(ct);

        // Two administrators writing the same model at once must not both see "no open
        // version" and open two. The lock is released with the transaction.
        await using (var candado = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended(@clave, 0))", conexion, transaccion))
        {
            candado.Parameters.AddWithValue("clave", $"asistente.tabla_de_precios:{empresa}/{modelo}");
            await candado.ExecuteNonQueryAsync(ct);
        }

        VersionDePrecio? abierta = null;
        await using (var lectura = new NpgsqlCommand(
            $"""
            SELECT {Columnas}
              FROM asistente.tabla_de_precios
             WHERE proveedor = @empresa AND modelo = @modelo AND vigente_hasta IS NULL
             ORDER BY vigente_desde DESC
             LIMIT 1
            """, conexion, transaccion))
        {
            lectura.Parameters.AddWithValue("empresa", empresa);
            lectura.Parameters.AddWithValue("modelo", modelo);
            await using var lector = await lectura.ExecuteReaderAsync(ct);
            if (await lector.ReadAsync(ct))
            {
                abierta = Leer(lector);
            }
        }

        if (abierta is not null && vigenteDesde < abierta.VigenteDesde)
        {
            return new EscrituraDePrecio(DesenlaceDeEscrituraDePrecio.VigenciaAnteriorALaVersionActual, abierta, null);
        }

        if (abierta is not null)
        {
            await using var cierre = new NpgsqlCommand(
                """
                UPDATE asistente.tabla_de_precios SET vigente_hasta = @hasta
                 WHERE proveedor = @empresa AND modelo = @modelo AND vigente_hasta IS NULL
                """, conexion, transaccion);
            cierre.Parameters.AddWithValue("hasta", vigenteDesde);
            cierre.Parameters.AddWithValue("empresa", empresa);
            cierre.Parameters.AddWithValue("modelo", modelo);
            await cierre.ExecuteNonQueryAsync(ct);
        }

        await using var alta = new NpgsqlCommand(
            $"""
            INSERT INTO asistente.tabla_de_precios
                (proveedor, modelo, precio_por_token_entrada, precio_por_token_salida,
                 precio_por_token_cache, version, vigente_desde, vigente_hasta)
            SELECT @empresa, @modelo, @entrada, @salida, @cache,
                   COALESCE(MAX(version), 0) + 1, @desde, NULL
              FROM asistente.tabla_de_precios
             WHERE proveedor = @empresa AND modelo = @modelo
            RETURNING {Columnas}
            """, conexion, transaccion);
        alta.Parameters.AddWithValue("empresa", empresa);
        alta.Parameters.AddWithValue("modelo", modelo);
        alta.Parameters.AddWithValue("entrada", entradaPorToken);
        alta.Parameters.AddWithValue("salida", salidaPorToken);
        alta.Parameters.AddWithValue("cache", cachePorToken);
        alta.Parameters.AddWithValue("desde", vigenteDesde);

        VersionDePrecio nueva;
        await using (var lector = await alta.ExecuteReaderAsync(ct))
        {
            await lector.ReadAsync(ct);
            nueva = Leer(lector);
        }

        await transaccion.CommitAsync(ct);
        return new EscrituraDePrecio(DesenlaceDeEscrituraDePrecio.Escrito, abierta, nueva);
    }

    private static VersionDePrecio Leer(NpgsqlDataReader lector) => new(
        lector.GetString(0),
        lector.GetString(1),
        lector.GetDecimal(2),
        lector.GetDecimal(3),
        lector.GetDecimal(4),
        lector.GetInt32(5),
        lector.GetFieldValue<DateTimeOffset>(6),
        lector.IsDBNull(7) ? null : lector.GetFieldValue<DateTimeOffset>(7));
}
