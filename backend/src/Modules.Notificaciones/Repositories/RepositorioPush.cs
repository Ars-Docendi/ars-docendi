using Microsoft.EntityFrameworkCore;
using Modules.Notificaciones.Domain;
using Modules.Notificaciones.Infrastructure;

namespace Modules.Notificaciones.Repositories;

/// <summary>Web Push subscriptions and per-module push preferences.</summary>
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
        // One atomic statement: concurrent upserts of the same endpoint (the browser re-syncs from
        // several places at once) never hit the unique constraint, so nothing is logged as an error.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO notificaciones.suscripciones_push (id, usuario_id, endpoint, p256dh, auth, creada_en)
            VALUES ({nueva.Id}, {nueva.UsuarioId}, {nueva.Endpoint}, {nueva.P256dh}, {nueva.Auth}, {nueva.CreadaEn})
            ON CONFLICT (endpoint) DO UPDATE
            SET usuario_id = EXCLUDED.usuario_id, p256dh = EXCLUDED.p256dh, auth = EXCLUDED.auth
            """, ct);
    }

    /// <summary>Only the owner's row: nobody can unsubscribe another user's device.</summary>
    public Task EliminarSuscripcionAsync(Guid usuarioId, string endpoint, CancellationToken ct) =>
        db.SuscripcionesPush.Where(s => s.UsuarioId == usuarioId && s.Endpoint == endpoint).ExecuteDeleteAsync(ct);

    public Task EliminarSuscripcionesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        db.SuscripcionesPush.Where(s => ids.Contains(s.Id)).ExecuteDeleteAsync(ct);

    /// <summary>Which of the users switched this module off for push.</summary>
    public async Task<ISet<Guid>> ListarQueDesactivaronAsync(
        IReadOnlyCollection<Guid> usuarioIds, string modulo, CancellationToken ct) =>
        (await db.PreferenciasPush.AsNoTracking()
            .Where(p => usuarioIds.Contains(p.UsuarioId) && p.Modulo == modulo && !p.Habilitado)
            .Select(p => p.UsuarioId)
            .ToListAsync(ct)).ToHashSet();

    public async Task<IReadOnlyDictionary<string, bool>> ListarPreferenciasAsync(Guid usuarioId, CancellationToken ct) =>
        await db.PreferenciasPush.AsNoTracking().Where(p => p.UsuarioId == usuarioId)
            .ToDictionaryAsync(p => p.Modulo, p => p.Habilitado, ct);

    public async Task GuardarPreferenciasAsync(
        Guid usuarioId, IReadOnlyDictionary<string, bool> cambios, CancellationToken ct)
    {
        var ahora = DateTimeOffset.UtcNow;
        var modulos = cambios.Keys.ToArray();
        var existentes = await db.PreferenciasPush
            .Where(p => p.UsuarioId == usuarioId && modulos.Contains(p.Modulo)).ToListAsync(ct);
        foreach (var (modulo, habilitado) in cambios)
        {
            var fila = existentes.FirstOrDefault(p => p.Modulo == modulo);
            if (fila is null)
            {
                db.PreferenciasPush.Add(new PreferenciaPush
                {
                    UsuarioId = usuarioId, Modulo = modulo, Habilitado = habilitado, ActualizadaEn = ahora,
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
