using Microsoft.EntityFrameworkCore;
using Modules.Aulas.Domain;
using Modules.Aulas.Infrastructure;

namespace Modules.Aulas.Repositories;

internal sealed class RepositorioSolicitudesAula(AulasDbContext db)
{
    public Task<SolicitudReservaAula?> ObtenerPorIdAsync(Guid id, CancellationToken ct) =>
        db.SolicitudesReserva.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<SolicitudReservaAula>> ListarPorDocenteAsync(
        Guid docenteId, CancellationToken ct) =>
        await db.SolicitudesReserva
                .AsNoTracking()
                .Where(s => s.DocenteId == docenteId)
                .OrderByDescending(s => s.CreadoEn)
                .ToListAsync(ct);

    public async Task<IReadOnlyList<SolicitudReservaAula>> ListarTodasAsync(CancellationToken ct) =>
        await db.SolicitudesReserva
                .AsNoTracking()
                .OrderByDescending(s => s.CreadoEn)
                .ToListAsync(ct);

    public void Agregar(SolicitudReservaAula solicitud) => db.SolicitudesReserva.Add(solicitud);

    public Task GuardarCambiosAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
