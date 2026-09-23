using System.Diagnostics;
using ArsDocendi.Shared.Identity;
using Microsoft.EntityFrameworkCore;

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
        catch (Exception)
        {
            return new ResultadoComprobacionBaseDatos(false, cronometro.Elapsed.TotalMilliseconds);
        }
    }
}

public sealed class ServicioEstadoSistema(IRepositorioEstadoSistema repositorio)
{
    public async Task<EstadoBaseDatosDto> ObtenerEstadoAsync(CancellationToken ct)
    {
        var resultado = await repositorio.ComprobarPostgreSqlAsync(ct);
        return new EstadoBaseDatosDto(
            resultado.Disponible ? "disponible" : "no_disponible",
            DateTimeOffset.UtcNow,
            resultado.DuracionMs);
    }
}
