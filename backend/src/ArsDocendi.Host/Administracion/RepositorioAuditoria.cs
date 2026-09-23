using ArsDocendi.Shared.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArsDocendi.Host.Administracion;

public sealed record ResultadoPaginaAuditoria(IReadOnlyList<RegistroCambio> Registros, long Total);

public interface IRepositorioAuditoria
{
    Task<ResultadoPaginaAuditoria> ListarAsync(ConsultaAuditoriaDto filtros, CancellationToken ct);
}

public sealed class RepositorioAuditoria(IdentityDbContext db) : IRepositorioAuditoria
{
    public async Task<ResultadoPaginaAuditoria> ListarAsync(
        ConsultaAuditoriaDto filtros,
        CancellationToken ct)
    {
        db.Database.SetCommandTimeout(TimeSpan.FromSeconds(5));
        var consulta = db.RegistrosDeCambio.AsNoTracking();
        if (filtros.Desde is { } desde) consulta = consulta.Where(r => r.CambiadoEn >= desde);
        if (filtros.Hasta is { } hasta) consulta = consulta.Where(r => r.CambiadoEn <= hasta);
        if (filtros.Accion is { Length: > 0 } accion) consulta = consulta.Where(r => r.Accion == accion);
        if (filtros.Schema is { Length: > 0 } schema) consulta = consulta.Where(r => r.NombreSchema == schema);
        if (filtros.Tabla is { Length: > 0 } tabla) consulta = consulta.Where(r => r.NombreTabla == tabla);
        if (filtros.CambiadoPor is { } actor) consulta = consulta.Where(r => r.CambiadoPor == actor);
        if (filtros.RowPk is { Length: > 0 } rowPk) consulta = consulta.Where(r => r.ClaveFila == rowPk);

        var total = await consulta.LongCountAsync(ct);
        var registros = await consulta
            .OrderByDescending(r => r.CambiadoEn)
            .ThenByDescending(r => r.Id)
            .Skip((filtros.Pagina - 1) * filtros.TamanoPagina)
            .Take(filtros.TamanoPagina)
            .ToListAsync(ct);
        return new ResultadoPaginaAuditoria(registros, total);
    }
}
