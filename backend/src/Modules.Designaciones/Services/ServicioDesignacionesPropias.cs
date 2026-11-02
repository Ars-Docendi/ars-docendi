using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using Modules.Designaciones.Api;
using Modules.Designaciones.Repositories;

namespace Modules.Designaciones.Services;

public interface IServicioDesignacionesPropias
{
    Task<IReadOnlyList<DesignacionPropiaDto>> ListarVigentesAsync(CancellationToken ct);
}

internal sealed class ServicioDesignacionesPropias(
    RepositorioDesignaciones repositorio,
    ICurrentUser usuarioActual,
    IConsultasIdentity identity) : IServicioDesignacionesPropias
{
    public async Task<IReadOnlyList<DesignacionPropiaDto>> ListarVigentesAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(usuarioActual.UserId, out var usuarioId)) return [];
        if ((await identity.ObtenerUsuarioAsync(usuarioId, ct))?.PersonaId is not { } personaId) return [];

        var designaciones = await repositorio.ListarVigentesDePersonaSinTrackingAsync(personaId, ct);
        if (designaciones.Count == 0) return [];

        var materias = (await identity.ListarMateriasAsync(ct)).ToDictionary(m => m.Id, m => m.Nombre);
        var carreras = (await identity.ListarMateriasPlanAsync(ct))
            .Where(mp => mp.Plan?.Carrera is not null)
            .Select(mp => (mp.Plan!.CarreraId, mp.Plan.Carrera!.Nombre))
            .DistinctBy(c => c.CarreraId)
            .ToDictionary(c => c.CarreraId, c => c.Nombre);

        return designaciones
            .Select(d => new DesignacionPropiaDto(
                d.Id,
                materias.GetValueOrDefault(d.MateriaId, "(materia no encontrada)"),
                carreras.GetValueOrDefault(d.CarreraId, ""),
                d.Cargo?.Nombre ?? "",
                d.DedicacionCatalogo?.Nombre ?? d.Dedicacion,
                d.Horas,
                d.HorasInvestigacion,
                d.HorasExternas,
                d.VigenteDesde))
            .OrderBy(d => d.Materia, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
