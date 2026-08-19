namespace Modules.Designaciones.Contracts.Administracion;

public sealed record CargoAdministracionDto(
    Guid Id,
    string Codigo,
    string Nombre,
    string Abreviatura,
    short Orden,
    bool Activo);

public sealed record DesignacionVigenteDto(
    Guid Id,
    Guid PersonaId,
    Guid MateriaId,
    Guid CargoId,
    string CargoNombre,
    string CargoAbreviatura,
    string? Dedicacion,
    int Horas,
    DateOnly VigenteDesde);

public sealed record GuardarDesignacionVigenteDto(
    Guid MateriaId,
    Guid CargoId,
    string? Dedicacion,
    int Horas);

/// <summary>
/// Frontera pública y pura para que la administración consulte y reemplace el
/// estado docente vigente sin acceder a entidades, repositorios ni DbContext del módulo.
/// </summary>
public interface IAdministracionDesignaciones
{
    Task<IReadOnlyList<DesignacionVigenteDto>> ListarVigentesAsync(CancellationToken ct);
    Task<IReadOnlyList<CargoAdministracionDto>> ListarCargosAsync(CancellationToken ct);
    Task ValidarReemplazoAsync(
        IReadOnlyList<GuardarDesignacionVigenteDto> designaciones,
        CancellationToken ct);
    Task<IReadOnlyList<DesignacionVigenteDto>> ReemplazarVigentesAsync(
        Guid personaId,
        IReadOnlyList<GuardarDesignacionVigenteDto> designaciones,
        CancellationToken ct);
}
