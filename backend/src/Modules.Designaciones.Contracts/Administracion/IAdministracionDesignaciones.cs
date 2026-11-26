namespace Modules.Designaciones.Contracts.Administracion;

public sealed record CargoAdministracionDto(
    Guid Id,
    string Codigo,
    string Nombre,
    string Abreviatura,
    short Orden,
    bool Activo);

public sealed record DedicacionAdministracionDto(Guid Id, short Codigo, string Nombre, short Orden, bool Activo);

public sealed record SectorExternoAdministracionDto(Guid Id, string Codigo, string Nombre, bool Activo);

/// <summary>
/// Designación vigente. Una designación externa trae el sector y deja cargo y dedicación en null;
/// una interna trae cargo y dedicación y deja el sector en null.
/// </summary>
public sealed record DesignacionVigenteDto(
    Guid Id,
    Guid PersonaId,
    Guid MateriaId,
    Guid CarreraId,
    Guid? CargoId,
    string? CargoNombre,
    string? CargoAbreviatura,
    string? Dedicacion,
    int Horas,
    DateOnly VigenteDesde,
    Guid? DedicacionId = null,
    int? HorasInvestigacion = null,
    int? HorasExternas = null,
    Guid? SectorExternoId = null,
    string? SectorExternoCodigo = null,
    string? SectorExternoNombre = null);

/// <summary>
/// Frontera pública y pura para que la administración consulte el estado docente vigente sin
/// acceder a entidades, repositorios ni DbContext del módulo. Es de sólo lectura: las
/// designaciones cambian únicamente por un pedido aprobado.
/// </summary>
public interface IAdministracionDesignaciones
{
    Task<IReadOnlyList<DesignacionVigenteDto>> ListarVigentesAsync(CancellationToken ct);
    Task<IReadOnlyList<CargoAdministracionDto>> ListarCargosAsync(CancellationToken ct);
    Task<IReadOnlyList<DedicacionAdministracionDto>> ListarDedicacionesAsync(CancellationToken ct);
    Task<IReadOnlyList<SectorExternoAdministracionDto>> ListarSectoresExternosAsync(CancellationToken ct);
}
