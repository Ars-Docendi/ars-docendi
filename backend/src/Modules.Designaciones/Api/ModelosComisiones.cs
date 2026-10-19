namespace Modules.Designaciones.Api;

public sealed record PeriodoLectivoGuaraniDto(string Id, string Nombre, int Anio);

public sealed record ImportarComisionesDto(string PeriodoLectivoId);

public sealed record ResultadoImportacionDto(
    Guid Id,
    int AnioAcademico,
    string PeriodoLectivoId,
    string PeriodoLectivoNombre,
    int Cantidad,
    int DocentesSinVincular,
    int CarrerasSinVincular);

public sealed record ImportacionResumenDto(
    DateTimeOffset Fecha,
    string Estado,
    string? Causa,
    string? Usuario);

public sealed record PeriodoImportadoDto(
    int AnioAcademico,
    string Id,
    string Nombre,
    ImportacionResumenDto? UltimaExitosa,
    ImportacionResumenDto? UltimoIntento);

public sealed record MateriaComisionDto(string Codigo, string Nombre, Guid? Id);

public sealed record CarreraComisionDto(string Codigo, string Nombre, Guid? Id);

public sealed record DocenteComisionDto(string Nombre, string? Responsabilidad, Guid? PersonaId);

/// <summary>Banda horaria; <c>Dia</c> va de 1 (lunes) a 7 (domingo), horas en <c>HH:mm</c>.</summary>
public sealed record HorarioComisionDto(string AsignacionId, short Dia, string Desde, string Hasta, string? Aula);

public sealed record ComisionDto(
    string Id,
    string Nombre,
    MateriaComisionDto Materia,
    IReadOnlyList<CarreraComisionDto> Carreras,
    IReadOnlyList<DocenteComisionDto> Docentes,
    IReadOnlyList<HorarioComisionDto> Horarios,
    string? ModalidadCodigo,
    string? Modalidad,
    string? Ubicacion);
