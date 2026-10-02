namespace Modules.Designaciones.Contracts.Administracion;

public sealed record CargoAdministracionDto(
    Guid Id,
    string Codigo,
    string Nombre,
    string Abreviatura,
    short Orden,
    bool Activo);

public sealed record DedicacionAdministracionDto(Guid Id, short Codigo, string Nombre, short Orden, bool Activo);

public sealed record DesignacionVigenteDto(
    Guid Id,
    Guid PersonaId,
    Guid MateriaId,
    Guid CargoId,
    string CargoNombre,
    string CargoAbreviatura,
    string? Dedicacion,
    int Horas,
    DateOnly VigenteDesde,
    Guid? DedicacionId = null,
    int? HorasInvestigacion = null,
    int? HorasExternas = null);

public sealed record GuardarDesignacionVigenteDto(
    Guid MateriaId,
    Guid CargoId,
    Guid? DedicacionId,
    int Horas);

/// <summary>
/// Frontera pública y pura para que la administración consulte y reemplace el
/// estado docente vigente sin acceder a entidades, repositorios ni DbContext del módulo.
/// </summary>
public interface IAdministracionDesignaciones
{
    Task<IReadOnlyList<DesignacionVigenteDto>> ListarVigentesAsync(CancellationToken ct);
    Task<IReadOnlyList<CargoAdministracionDto>> ListarCargosAsync(CancellationToken ct);
    Task<IReadOnlyList<DedicacionAdministracionDto>> ListarDedicacionesAsync(CancellationToken ct);
    Task ValidarReemplazoAsync(
        Guid? personaId,
        IReadOnlyList<GuardarDesignacionVigenteDto> designaciones,
        CancellationToken ct);
    Task<IReadOnlyList<DesignacionVigenteDto>> ReemplazarVigentesAsync(
        Guid personaId,
        IReadOnlyList<GuardarDesignacionVigenteDto> designaciones,
        CancellationToken ct);
}
