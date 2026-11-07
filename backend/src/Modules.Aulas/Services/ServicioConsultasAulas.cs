using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using Modules.Aulas.Contracts.Queries;
using Modules.Aulas.Repositories;

namespace Modules.Aulas.Services;

/// <summary>Consultas públicas de Aulas para otros módulos.</summary>
internal sealed class ServicioConsultasAulas(
    RepositorioSolicitudesAula repositorio,
    ICurrentUser usuarioActual,
    IConsultasIdentity identity) : IAulasQueries
{
    public async Task<IReadOnlySet<Guid>> SolicitudesAbriblesAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var permisos = usuarioActual.Permissions;
        var resuelve = permisos.Contains(Permisos.AulasAprobar);
        var solicita = permisos.Contains(Permisos.AulasSolicitar);

        // Sin ninguno de los dos permisos no se toca la base: el detalle respondería 404.
        if (ids.Count == 0 || (!resuelve && !solicita))
        {
            return new HashSet<Guid>();
        }

        var existentes = await repositorio.DuenosDeAsync(ids, ct);

        if (resuelve)
        {
            return existentes.Select(s => s.Id).ToHashSet();
        }

        // Mismo criterio que `ObtenerDetalleAsync`: la solicitud es del actor.
        if (!Guid.TryParse(usuarioActual.UserId, out var usuarioId))
        {
            return new HashSet<Guid>();
        }

        var personaActual = (await identity.ObtenerUsuarioAsync(usuarioId, ct))?.PersonaId;

        return existentes.Where(s => s.DocenteId == personaActual).Select(s => s.Id).ToHashSet();
    }
}
