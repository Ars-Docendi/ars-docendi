using System.Text.Json;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modules.Notificaciones.Contracts;
using Modules.Notificaciones.Domain;
using Modules.Notificaciones.Repositories;

namespace Modules.Notificaciones.Services;

/// <summary>
/// Write side. Best effort by contract: it never throws into the business flow, because it is
/// called after the business transaction committed and a lost notice must not undo it.
/// <para>
/// It works inside its OWN dependency-injection scope, hence with its own database contexts: it
/// never reuses a context or transaction of the caller, which may be failed or disposed. It does
/// not honor the request's cancellation (a client disconnecting after the commit must not drop the
/// notice) and uses a short timeout of its own instead. Failures are logged with the event type,
/// the entity id and the exception type only: never with titles, messages, names or database
/// error details, which can carry personal data.
/// </para>
/// </summary>
internal sealed class Notificador(
    IServiceScopeFactory scopes,
    ICurrentUser usuarioActual,
    ILogger<Notificador> logger) : INotificador
{
    private static readonly TimeSpan TiempoMaximo = TimeSpan.FromSeconds(10);

    public async Task NotificarAsync(
        AvisoNuevo aviso, IReadOnlyCollection<DestinoNotificacion> destinos, CancellationToken ct)
    {
        try
        {
            var actor = Guid.TryParse(usuarioActual.UserId, out var id) ? id : (Guid?)null;
            using var limite = new CancellationTokenSource(TiempoMaximo);
            await using var scope = scopes.CreateAsyncScope();
            var identity = scope.ServiceProvider.GetRequiredService<IConsultasIdentity>();
            var repositorio = scope.ServiceProvider.GetRequiredService<RepositorioNotificaciones>();

            var destinatarios = await ResolverAsync(aviso, destinos, actor, identity, limite.Token);
            if (destinatarios.Count == 0) return;

            var ahora = DateTimeOffset.UtcNow;
            var contexto = JsonSerializer.Serialize(aviso.Contexto);
            repositorio.Agregar(destinatarios.Select(usuarioId => new Notificacion
            {
                Id = Guid.NewGuid(),
                DestinatarioUsuarioId = usuarioId,
                RolDestino = aviso.RolDestino,
                TipoEvento = aviso.TipoEvento,
                TipoEntidad = aviso.TipoEntidad,
                EntidadId = aviso.EntidadId,
                Titulo = aviso.Titulo,
                Mensaje = aviso.Mensaje,
                Contexto = contexto,
                CreadaEn = ahora,
            }));
            await repositorio.GuardarCambiosAsync(limite.Token);
        }
        catch (Exception ex)
        {
            logger.LogError(
                "No se emitió el aviso {TipoEvento} de la entidad {EntidadId}; la acción ya estaba confirmada ({TipoError})",
                aviso.TipoEvento, aviso.EntidadId, ex.GetType().Name);
        }
    }

    /// <summary>Distinct active usuarios, without the actor.</summary>
    private async Task<IReadOnlyCollection<Guid>> ResolverAsync(
        AvisoNuevo aviso, IReadOnlyCollection<DestinoNotificacion> destinos, Guid? actor,
        IConsultasIdentity identity, CancellationToken ct)
    {
        // Usuarios named directly or through a persona may be inactive; those held by a role are
        // already filtered to active ones by the query.
        var nombrados = new HashSet<Guid>();
        var porRol = new HashSet<Guid>();

        foreach (var usuario in destinos.OfType<DestinoNotificacion.Usuario>())
            nombrados.Add(usuario.UsuarioId);

        var personas = destinos.OfType<DestinoNotificacion.Persona>().Select(p => p.PersonaId).Distinct().ToArray();
        if (personas.Length > 0)
        {
            var usuariosDePersonas = await identity.ObtenerUsuarioIdsDePersonasAsync(personas, ct);
            var sinUsuario = personas.Count(p => !usuariosDePersonas.ContainsKey(p));
            if (sinUsuario > 0)
            {
                logger.LogWarning(
                    "Aviso {TipoEvento} omitido para {Cantidad} persona(s) sin usuario", aviso.TipoEvento, sinUsuario);
            }
            nombrados.UnionWith(usuariosDePersonas.Values);
        }

        foreach (var rol in destinos.OfType<DestinoNotificacion.RolEnAmbito>())
        {
            porRol.UnionWith(await identity.ObtenerUsuarioIdsConRolAsync(
                rol.CodigoRol, rol.MateriaId, rol.CarreraId, ct));
        }

        if (nombrados.Count > 0)
        {
            var activos = (await identity.ObtenerUsuariosAsync(nombrados.ToArray(), ct))
                .Where(u => u.Activo).Select(u => u.Id);
            porRol.UnionWith(activos);
        }

        // The actor already knows what they just did.
        if (actor is { } excluido) porRol.Remove(excluido);
        return porRol;
    }
}
