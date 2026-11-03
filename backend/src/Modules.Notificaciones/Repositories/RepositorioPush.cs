using Microsoft.EntityFrameworkCore;
using Modules.Notificaciones.Domain;
using Modules.Notificaciones.Infrastructure;

namespace Modules.Notificaciones.Repositories;

/// <summary>Web Push subscriptions and per-type push preferences.</summary>
internal sealed class RepositorioPush(NotificacionesDbContext db)
{
    public async Task<IReadOnlyList<SuscripcionPush>> ListarSuscripcionesAsync(
        IReadOnlyCollection<Guid> usuarioIds, CancellationToken ct) =>
        await db.SuscripcionesPush.AsNoTracking().Where(s => usuarioIds.Contains(s.UsuarioId)).ToListAsync(ct);

    /// <summary>
    /// Upsert by endpoint. An endpoint held by another user is REASSIGNED: a device belongs to whoever
    /// is logged in on it, and the same browser subscription must not push to the previous user.
    /// </summary>
    public async Task GuardarSuscripcionAsync(SuscripcionPush nueva, CancellationToken ct)
    {
        if (await ActualizarAsync(nueva, ct) > 0) return;

        db.SuscripcionesPush.Add(nueva);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost a race against a concurrent upsert of the same endpoint: reassign that row instead.
            db.Entry(nueva).State = EntityState.Detached;
            await ActualizarAsync(nueva, ct);
        }
    }

    private Task<int> ActualizarAsync(SuscripcionPush nueva, CancellationToken ct) =>
        db.SuscripcionesPush.Where(s => s.Endpoint == nueva.Endpoint)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.UsuarioId, nueva.UsuarioId)
                .SetProperty(s => s.P256dh, nueva.P256dh)
                .SetProperty(s => s.Auth, nueva.Auth), ct);

    /// <summary>Only the owner's row: nobody can unsubscribe another user's device.</summary>
    public Task EliminarSuscripcionAsync(Guid usuarioId, string endpoint, CancellationToken ct) =>
        db.SuscripcionesPush.Where(s => s.UsuarioId == usuarioId && s.Endpoint == endpoint).ExecuteDeleteAsync(ct);

    public Task EliminarSuscripcionesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        db.SuscripcionesPush.Where(s => ids.Contains(s.Id)).ExecuteDeleteAsync(ct);

    /// <summary>Which of the users switched this event type off for push.</summary>
    public async Task<ISet<Guid>> ListarQueDesactivaronAsync(
        IReadOnlyCollection<Guid> usuarioIds, string tipoEvento, CancellationToken ct) =>
        (await db.PreferenciasPush.AsNoTracking()
            .Where(p => usuarioIds.Contains(p.UsuarioId) && p.TipoEvento == tipoEvento && !p.Habilitado)
            .Select(p => p.UsuarioId)
            .ToListAsync(ct)).ToHashSet();

    public async Task<IReadOnlyDictionary<string, bool>> ListarPreferenciasAsync(Guid usuarioId, CancellationToken ct) =>
        await db.PreferenciasPush.AsNoTracking().Where(p => p.UsuarioId == usuarioId)
            .ToDictionaryAsync(p => p.TipoEvento, p => p.Habilitado, ct);

    public async Task GuardarPreferenciasAsync(
        Guid usuarioId, IReadOnlyDictionary<string, bool> cambios, CancellationToken ct)
    {
        var ahora = DateTimeOffset.UtcNow;
        var tipos = cambios.Keys.ToArray();
        var existentes = await db.PreferenciasPush
            .Where(p => p.UsuarioId == usuarioId && tipos.Contains(p.TipoEvento)).ToListAsync(ct);
        foreach (var (tipo, habilitado) in cambios)
        {
            var fila = existentes.FirstOrDefault(p => p.TipoEvento == tipo);
            if (fila is null)
            {
                db.PreferenciasPush.Add(new PreferenciaPush
                {
                    UsuarioId = usuarioId, TipoEvento = tipo, Habilitado = habilitado, ActualizadaEn = ahora,
                });
            }
            else
            {
                fila.Habilitado = habilitado;
                fila.ActualizadaEn = ahora;
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
