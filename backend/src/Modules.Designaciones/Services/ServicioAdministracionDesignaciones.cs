using Modules.Designaciones.Contracts.Administracion;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Repositories;

namespace Modules.Designaciones.Services;

internal sealed class ServicioAdministracionDesignaciones(RepositorioDesignaciones repositorio, TimeProvider? reloj = null)
    : IAdministracionDesignaciones
{
    public async Task<IReadOnlyList<DesignacionVigenteDto>> ListarVigentesAsync(CancellationToken ct) =>
        (await repositorio.ListarTodasVigentesSinTrackingAsync(ct)).Select(Mapear).ToArray();

    public async Task<IReadOnlyList<CargoAdministracionDto>> ListarCargosAsync(CancellationToken ct) =>
        (await repositorio.ListarCargosAsync(ct)).Select(Mapear).ToArray();

    public async Task<IReadOnlyList<DedicacionAdministracionDto>> ListarDedicacionesAsync(CancellationToken ct) =>
        (await repositorio.ListarDedicacionesAsync(ct))
            .Select(d => new DedicacionAdministracionDto(d.Id, d.Codigo, d.Nombre, d.Orden, d.Activo)).ToArray();

    public async Task<IReadOnlyList<SectorExternoAdministracionDto>> ListarSectoresExternosAsync(CancellationToken ct) =>
        (await repositorio.ListarSectoresExternosAsync(ct))
            .Select(s => new SectorExternoAdministracionDto(s.Id, s.Codigo, s.Nombre, s.Activo)).ToArray();

    private static DesignacionVigenteDto Mapear(Designacion d) => new(
        d.Id,
        d.PersonaId,
        d.MateriaId,
        d.CarreraId,
        d.CargoId,
        d.Cargo?.Nombre,
        d.Cargo?.Abreviatura,
        d.DedicacionCatalogo?.Nombre,
        d.Horas,
        d.VigenteDesde,
        d.DedicacionId, d.HorasInvestigacion, d.HorasExternas,
        d.SectorExternoId, d.SectorExterno?.Codigo, d.SectorExterno?.Nombre);

    private static CargoAdministracionDto Mapear(Cargo c) => new(
        c.Id, c.Codigo, c.Nombre, c.Abreviatura, c.Orden, c.Activo);
}
