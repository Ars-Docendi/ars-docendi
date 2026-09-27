using System.Data.Common;
using System.Diagnostics;
using ArsDocendi.Shared.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Asistente.Contracts;

namespace ArsDocendi.Host.Administracion;

public interface IRepositorioEstadoSistema
{
    Task<ResultadoComprobacionBaseDatos> ComprobarPostgreSqlAsync(CancellationToken ct);
}

public sealed record ResultadoComprobacionBaseDatos(bool Disponible, double DuracionMs);

public sealed class RepositorioEstadoSistema(IdentityDbContext db) : IRepositorioEstadoSistema
{
    public async Task<ResultadoComprobacionBaseDatos> ComprobarPostgreSqlAsync(CancellationToken ct)
    {
        var cronometro = Stopwatch.StartNew();
        try
        {
            db.Database.SetCommandTimeout(TimeSpan.FromSeconds(3));
            var resultado = await db.Database
                .SqlQueryRaw<int>("SELECT 1 AS \"Value\"")
                .SingleAsync(ct);
            return new ResultadoComprobacionBaseDatos(resultado == 1, cronometro.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (DbException)
        {
            return new ResultadoComprobacionBaseDatos(false, cronometro.Elapsed.TotalMilliseconds);
        }
        catch (TimeoutException)
        {
            return new ResultadoComprobacionBaseDatos(false, cronometro.Elapsed.TotalMilliseconds);
        }
        catch (InvalidOperationException ex) when (ContieneFalloDeBaseDatos(ex))
        {
            return new ResultadoComprobacionBaseDatos(false, cronometro.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException)
        {
            return new ResultadoComprobacionBaseDatos(false, cronometro.Elapsed.TotalMilliseconds);
        }
    }

    private static bool ContieneFalloDeBaseDatos(Exception excepcion)
    {
        for (Exception? actual = excepcion; actual is not null; actual = actual.InnerException)
        {
            if (actual is DbException or TimeoutException) return true;
        }
        return false;
    }
}

/// <summary>
/// Orquesta la comprobación de PostgreSQL y del modo mantenimiento del
/// asistente (sistema-seccion-unificada, design.md D7).
/// </summary>
/// <remarks>
/// Las dos comprobaciones corren CONCURRENTES y con techos independientes: una
/// falla nunca demora ni contamina a la otra. El techo de mantenimiento es
/// propio y más chico que el de PostgreSQL (repositorio.ComprobarPostgreSqlAsync
/// ya trae el suyo, 3 s) — dos servicios lentos no deberían sumar sus esperas.
/// </remarks>
public sealed class ServicioEstadoSistema(
    IRepositorioEstadoSistema repositorio,
    IConsultaDeMantenimiento consultaDeMantenimiento,
    ILogger<ServicioEstadoSistema> log)
{
    private static readonly TimeSpan TimeoutMantenimiento = TimeSpan.FromSeconds(3);

    public async Task<EstadoSistemaDto> ObtenerEstadoAsync(CancellationToken ct)
    {
        var tareaBaseDeDatos = repositorio.ComprobarPostgreSqlAsync(ct);
        var tareaMantenimiento = ConsultarMantenimientoAsync(ct);

        await Task.WhenAll(tareaBaseDeDatos, tareaMantenimiento);

        var resultado = await tareaBaseDeDatos;
        return new EstadoSistemaDto(
            resultado.Disponible ? "disponible" : "no_disponible",
            DateTimeOffset.UtcNow,
            resultado.DuracionMs,
            await tareaMantenimiento);
    }

    /// <summary>
    /// <c>activo</c>/<c>inactivo</c>, o <c>desconocido</c> ante cualquier falla
    /// o vencimiento de su propio techo — nunca deja caer la comprobación
    /// completa por un fallo del asistente (design.md D7).
    /// </summary>
    private async Task<string> ConsultarMantenimientoAsync(CancellationToken ct)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeoutMantenimiento);

        try
        {
            var estado = await consultaDeMantenimiento.ConsultarAsync(limite.Token);
            return estado.Activo ? "activo" : "inactivo";
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Venció el techo PROPIO de mantenimiento, no la cancelación del
            // caller — no se relanza.
            log.LogWarning(
                "El estado de mantenimiento del asistente no respondió dentro de {TimeoutSegundos}s.",
                TimeoutMantenimiento.TotalSeconds);
            return "desconocido";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "No se pudo consultar el estado de mantenimiento del asistente.");
            return "desconocido";
        }
    }
}
