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

    /// <summary>De los ids dados, el de cada solicitud que existe con el de su dueña.</summary>
    public async Task<IReadOnlyList<(Guid Id, Guid DocenteId)>> DuenosDeAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        (await db.SolicitudesReserva
                .AsNoTracking()
                .Where(s => ids.Contains(s.Id))
                .Select(s => new { s.Id, s.DocenteId })
                .ToListAsync(ct))
            .Select(s => (s.Id, s.DocenteId))
            .ToList();

    public void Agregar(SolicitudReservaAula solicitud) => db.SolicitudesReserva.Add(solicitud);

    public Task GuardarCambiosAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
