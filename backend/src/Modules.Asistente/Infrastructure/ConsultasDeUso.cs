using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Persistencia;
using Modules.Asistente.Application;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// <see cref="IConsultasDeUso"/> sobre <c>asistente.registro_operativo</c> y
/// <c>asistente.tabla_de_precios</c> (asistente-panel-de-uso, design.md D12
/// de asistente-administracion-de-uso).
/// </summary>
/// <remarks>
/// Agrega por ROL sumando la fila de cada actor a TODOS los roles de sistema
/// que tiene vigentes — decidido acá porque design.md no fija la regla: un
/// actor con dos roles usó el asistente con las dos identidades a la vez, así
/// que su uso cuenta para las dos, igual que un permiso efectivo es la unión
/// de todos los roles vigentes (<c>IConsultasIdentity.ObtenerCodigosDePermisosAsync</c>
/// ya sigue el mismo criterio).
/// </remarks>
internal sealed class ConsultasDeUso(CadenaDuena cadena, IConsultasIdentity identidad) : IConsultasDeUso
{
    public async Task<PanelDeUso> ObtenerAsync(RangoDePeriodo periodo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(periodo);

        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        var filas = await LeerFilasAsync(conexion, periodo, ct);
        var precios = await LeerPreciosAsync(conexion, ct);
        var nombres = (await identidad.ListarUsuariosAsync(ct))
            .ToDictionary(u => u.Id, u => u.NombreParaMostrar);

        var porUsuario = filas
            .GroupBy(f => f.Actor)
            .Select(g => Agregar(
                g.Key.ToString(), nombres.GetValueOrDefault(g.Key), [.. g], precios))
            .OrderBy(a => a.Clave, StringComparer.Ordinal)
            .ToList();

        var porRol = new Dictionary<string, List<Fila>>(StringComparer.Ordinal);
        foreach (var fila in filas)
        {
            foreach (var rol in await identidad.ObtenerCodigosDeRolesDeSistemaAsync(fila.Actor, ct))
            {
                if (!porRol.TryGetValue(rol, out var lista))
                {
                    lista = [];
                    porRol[rol] = lista;
                }

                lista.Add(fila);
            }
        }

        var agregadosPorRol = porRol
            .Select(par => Agregar(par.Key, null, par.Value, precios))
            .OrderBy(a => a.Clave, StringComparer.Ordinal)
            .ToList();

        var organizacion = Agregar("organizacion", null, filas, precios);

        return new PanelDeUso(porUsuario, agregadosPorRol, organizacion);
    }

    private static UsoAgregado Agregar(
        string clave, string? nombre, IReadOnlyList<Fila> filas, IReadOnlyList<PrecioVigente> precios)
    {
        var porEstado = filas
            .GroupBy(f => f.Estado, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var latencias = filas.Select(f => (double)f.LatenciaMs).Order().ToList();

        var costeo = CalculadoraDeCosto.Costear(
            [.. filas.Select(f => new FilaDeConsumo(
                f.Proveedor, f.OcurridoEn, f.TokensDeEntrada, f.TokensDeSalida, f.TokensDeCache))],
            precios);

        return new UsoAgregado(
            clave,
            nombre,
            filas.Count,
            porEstado,
            filas.Sum(f => f.LlamadasAlModelo),
            filas.Sum(f => (long)f.TokensDeEntrada),
            filas.Sum(f => (long)f.TokensDeSalida),
            filas.Sum(f => (long)(f.TokensDeCache ?? 0)),
            latencias.Count == 0 ? 0 : latencias.Average(),
            Percentil95(latencias),
            [.. filas.Select(f => f.Proveedor).Where(p => p is not null).Distinct().Order()!],
            costeo.CostoEstimadoTotal,
            costeo.FilasSinPrecio);
    }

    /// <summary>El percentil 95, por interpolación del rango más cercano.</summary>
    private static double Percentil95(List<double> ordenadas)
    {
        if (ordenadas.Count == 0)
        {
            return 0;
        }

        var indice = (int)Math.Ceiling(0.95 * ordenadas.Count) - 1;
        return ordenadas[Math.Clamp(indice, 0, ordenadas.Count - 1)];
    }

    private static async Task<List<Fila>> LeerFilasAsync(
        NpgsqlConnection conexion, RangoDePeriodo periodo, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(
            """
            SELECT actor_id, ocurrido_en, estado, llamadas_al_modelo, tokens_de_entrada,
                   tokens_de_salida, tokens_de_cache, latencia_ms, proveedor
              FROM asistente.registro_operativo
             WHERE ocurrido_en >= @desde AND ocurrido_en < @hasta
            """, conexion);
        comando.Parameters.AddWithValue("desde", periodo.Desde);
        comando.Parameters.AddWithValue("hasta", periodo.Hasta);

        var filas = new List<Fila>();
        await using var lector = await comando.ExecuteReaderAsync(ct);
        while (await lector.ReadAsync(ct))
        {
            filas.Add(new Fila(
                lector.GetGuid(0),
                lector.GetFieldValue<DateTimeOffset>(1),
                lector.GetString(2),
                lector.GetInt32(3),
                lector.GetInt32(4),
                lector.GetInt32(5),
                lector.IsDBNull(6) ? null : lector.GetInt32(6),
                lector.GetInt32(7),
                lector.IsDBNull(8) ? null : lector.GetString(8)));
        }

        return filas;
    }

    private static async Task<List<PrecioVigente>> LeerPreciosAsync(
        NpgsqlConnection conexion, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(
            """
            SELECT proveedor, modelo, precio_por_token_entrada, precio_por_token_salida,
                   precio_por_token_cache, version, vigente_desde, vigente_hasta
              FROM asistente.tabla_de_precios
            """, conexion);

        var precios = new List<PrecioVigente>();
        await using var lector = await comando.ExecuteReaderAsync(ct);
        while (await lector.ReadAsync(ct))
        {
            precios.Add(new PrecioVigente(
                lector.GetString(0),
                lector.GetString(1),
                lector.GetDecimal(2),
                lector.GetDecimal(3),
                lector.GetDecimal(4),
                lector.GetInt32(5),
                lector.GetFieldValue<DateTimeOffset>(6),
                lector.IsDBNull(7) ? null : lector.GetFieldValue<DateTimeOffset>(7)));
        }

        return precios;
    }

    private sealed record Fila(
        Guid Actor,
        DateTimeOffset OcurridoEn,
        string Estado,
        int LlamadasAlModelo,
        int TokensDeEntrada,
        int TokensDeSalida,
        int? TokensDeCache,
        int LatenciaMs,
        string? Proveedor);
}
