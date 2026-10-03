using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Logging;
using Modules.Asistente.Application;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// <see cref="IPresupuestoOrganizacional"/> sobre Postgres (design.md D3/D6
/// de asistente-administracion-de-uso).
/// </summary>
/// <remarks>
/// Pide <see cref="CadenaDuena"/> por el mismo motivo que
/// <see cref="CuotaPersistente"/>: <c>tope_organizacional</c>,
/// <c>consumo_organizacional_mensual</c> y <c>tabla_de_precios</c> viven en el
/// schema <c>asistente</c>, revocado entero a los dos roles de solo lectura.
/// </remarks>
internal sealed class PresupuestoOrganizacionalPersistente(
    CadenaDuena cadena, TimeProvider reloj, DetectorDeUmbrales detector, ILogger<PresupuestoOrganizacionalPersistente> log)
    : IPresupuestoOrganizacional
{
    public async Task<bool> HayPresupuestoAsync(CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        var tope = await TopeVigenteAsync(conexion, ct);
        if (tope <= 0)
        {
            return true;
        }

        var ahora = reloj.GetUtcNow();
        var acumulado = await AcumuladoDelMesAsync(conexion, ahora.Year, ahora.Month, ct);

        return acumulado < tope;
    }

    public async Task AcumularAsync(
        string? proveedor,
        DateTimeOffset ocurridoEn,
        int tokensDeEntrada,
        int tokensDeSalida,
        int? tokensDeCache,
        CancellationToken ct)
    {
        if (proveedor is null)
        {
            return;
        }

        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        // El precio VIGENTE AHORA, no el histórico de `ocurridoEn` (design.md
        // D6): el acumulador incrementa una vez, en el momento del cobro, con
        // el precio que rige en ese instante. El panel de uso (grupo 9) es
        // quien re-costea contra el histórico para mostrarle al admin la
        // estimación de una fila pasada.
        var precio = await PrecioVigenteAhoraAsync(conexion, proveedor, ct);
        if (precio is null)
        {
            // Sin precio, no hay nada que sumar — un proveedor/modelo sin
            // fila en tabla_de_precios no puede aportar al tope organizacional
            // (mismo criterio de "no costear en cero" de CalculadoraDeCosto).
            // No poder tapar el tope con un proveedor sin precio es una
            // limitación conocida, no un bug: el panel de uso (grupo 9) sigue
            // reportando esas filas como "sin precio", visibles.
            return;
        }

        var fila = new FilaDeConsumo(proveedor, ocurridoEn, tokensDeEntrada, tokensDeSalida, tokensDeCache);
        var costo = CalculadoraDeCosto.CostoDeUnaFila(fila, [precio]) ?? 0m;

        var ahora = reloj.GetUtcNow();

        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO asistente.consumo_organizacional_mensual (anio, mes, costo_estimado_acumulado)
            VALUES (@anio, @mes, @costo)
            ON CONFLICT (anio, mes)
            DO UPDATE SET costo_estimado_acumulado = asistente.consumo_organizacional_mensual.costo_estimado_acumulado + @costo
            RETURNING costo_estimado_acumulado
            """, conexion);
        comando.Parameters.AddWithValue("anio", ahora.Year);
        comando.Parameters.AddWithValue("mes", ahora.Month);
        comando.Parameters.AddWithValue("costo", costo);

        var acumulado = (decimal)(await comando.ExecuteScalarAsync(ct))!;

        var tope = await TopeVigenteAsync(conexion, ct);
        if (tope > 0)
        {
            detector.RegistrarSiCorresponde(
                log, "organizacion", "global", $"{ahora.Year:D4}-{ahora.Month:D2}",
                (double)(100.0m * acumulado / tope));
        }
    }

    private static async Task<decimal> TopeVigenteAsync(NpgsqlConnection conexion, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(
            """
            SELECT tope_mensual_usd
              FROM asistente.tope_organizacional
             WHERE vigente_desde <= now()
             ORDER BY vigente_desde DESC
             LIMIT 1
            """, conexion);

        var valor = await comando.ExecuteScalarAsync(ct);
        return valor is null or DBNull ? 0m : (decimal)valor;
    }

    private static async Task<decimal> AcumuladoDelMesAsync(
        NpgsqlConnection conexion, int anio, int mes, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(
            """
            SELECT costo_estimado_acumulado
              FROM asistente.consumo_organizacional_mensual
             WHERE anio = @anio AND mes = @mes
            """, conexion);
        comando.Parameters.AddWithValue("anio", anio);
        comando.Parameters.AddWithValue("mes", mes);

        var valor = await comando.ExecuteScalarAsync(ct);
        return valor is null or DBNull ? 0m : (decimal)valor;
    }

    private static async Task<PrecioVigente?> PrecioVigenteAhoraAsync(
        NpgsqlConnection conexion, string proveedorYModelo, CancellationToken ct)
    {
        var separador = proveedorYModelo.IndexOf('/');
        if (separador < 0)
        {
            return null;
        }

        var proveedor = proveedorYModelo[..separador];
        var modelo = proveedorYModelo[(separador + 1)..];

        await using var comando = new NpgsqlCommand(
            """
            SELECT precio_por_token_entrada, precio_por_token_salida, precio_por_token_cache, version, vigente_desde, vigente_hasta
              FROM asistente.tabla_de_precios
             WHERE proveedor = @proveedor
               AND modelo = @modelo
               AND vigente_desde <= now()
               AND (vigente_hasta IS NULL OR now() < vigente_hasta)
             ORDER BY vigente_desde DESC
             LIMIT 1
            """, conexion);
        comando.Parameters.AddWithValue("proveedor", proveedor);
        comando.Parameters.AddWithValue("modelo", modelo);

        await using var lector = await comando.ExecuteReaderAsync(ct);
        if (!await lector.ReadAsync(ct))
        {
            return null;
        }

        return new PrecioVigente(
            proveedor,
            modelo,
            lector.GetDecimal(0),
            lector.GetDecimal(1),
            lector.GetDecimal(2),
            lector.GetInt32(3),
            lector.GetFieldValue<DateTimeOffset>(4),
            lector.IsDBNull(5) ? null : lector.GetFieldValue<DateTimeOffset>(5));
    }
}
