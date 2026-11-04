using Microsoft.EntityFrameworkCore;
using Modules.Notificaciones.Domain;
using Modules.Notificaciones.Infrastructure;

namespace Modules.Notificaciones.Repositories;

internal sealed class RepositorioNotificaciones(NotificacionesDbContext db)
{
    /// <summary>
    /// A notification is visible to its recipient, and only under the profile it was addressed to
    /// (<c>null</c> means any profile). Every read and write goes through this filter, so "another
    /// user's" and "another profile's" notices are indistinguishable from missing ones.
    /// </summary>
    private IQueryable<Notificacion> Visibles(Guid usuarioId, string? perfil) =>
        db.Notificaciones.Where(n => n.DestinatarioUsuarioId == usuarioId
                                  && (n.RolDestino == null || n.RolDestino == perfil));

    public Task<int> ContarNoLeidasAsync(Guid usuarioId, string? perfil, CancellationToken ct) =>
        Visibles(usuarioId, perfil).CountAsync(n => n.LeidaEn == null, ct);

    public async Task<(IReadOnlyList<Notificacion> Datos, int Total)> ListarAsync(
        Guid usuarioId, string? perfil, bool? leidas, int pagina, int tamanio, CancellationToken ct)
    {
        var consulta = Visibles(usuarioId, perfil).AsNoTracking();
        if (leidas is { } soloLeidas)
            consulta = consulta.Where(n => (n.LeidaEn != null) == soloLeidas);

        var total = await consulta.CountAsync(ct);
        var datos = await consulta
            .OrderByDescending(n => n.CreadaEn).ThenByDescending(n => n.Id)
            .Skip((pagina - 1) * tamanio).Take(tamanio)
            .ToListAsync(ct);
        return (datos, total);
    }

    /// <summary>
    /// Sets the read instant if empty and returns the notification, or <c>null</c> when it is not
    /// visible. The conditional UPDATE keeps the first instant under concurrent calls.
    /// </summary>
    public async Task<Notificacion?> MarcarLeidaAsync(
        Guid usuarioId, string? perfil, Guid id, DateTimeOffset ahora, CancellationToken ct)
    {
        await Visibles(usuarioId, perfil)
            .Where(n => n.Id == id && n.LeidaEn == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.LeidaEn, ahora), ct);
        return await Visibles(usuarioId, perfil).AsNoTracking().FirstOrDefaultAsync(n => n.Id == id, ct);
    }

    /// <summary>Marks the selected visible unread notices; returns how many changed.</summary>
    public Task<int> MarcarLeidasAsync(
        Guid usuarioId, string? perfil, IReadOnlyCollection<Guid>? ids, DateTimeOffset? hasta,
        DateTimeOffset ahora, CancellationToken ct)
    {
        var consulta = Visibles(usuarioId, perfil).Where(n => n.LeidaEn == null);
        if (ids is not null) consulta = consulta.Where(n => ids.Contains(n.Id));
        if (hasta is { } corte) consulta = consulta.Where(n => n.CreadaEn <= corte);
        return consulta.ExecuteUpdateAsync(s => s.SetProperty(n => n.LeidaEn, ahora), ct);
    }

    public void Agregar(IEnumerable<Notificacion> filas) => db.Notificaciones.AddRange(filas);

    public Task GuardarCambiosAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
