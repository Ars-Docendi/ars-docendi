namespace Modules.Designaciones.Api;

/// <summary>Cargo, dedicación y las tres horas de una designación o de lo que pide un pedido.</summary>
public sealed record ValoresDesignacionDto(
    string? Cargo,
    string? Dedicacion,
    int? Horas,
    int? HorasInvestigacion,
    int? HorasExternas);

/// <summary>Una designación vigente del docente, en cualquier materia.</summary>
public sealed record DesignacionVigenteDto(
    Guid MateriaId,
    string Materia,
    Guid CarreraId,
    string Carrera,
    ValoresDesignacionDto Valores);

/// <summary>
/// El pedido del docente en la conciliación. <see cref="Hoy"/> es lo que tiene en la
/// materia del pedido antes de sincronizar (nulo en un Alta); <see cref="Queda"/>, lo que
/// queda (nulo en una Baja).
/// </summary>
public sealed record PedidoConciliacionDto(
    Guid Id,
    string Numero,
    uint Version,
    string Novedad,
    string Estado,
    Guid MateriaId,
    string Materia,
    Guid CarreraId,
    string Carrera,
    ValoresDesignacionDto? Hoy,
    ValoresDesignacionDto? Queda,
    string? Justificacion,
    string? TipoBaja,
    string? TipoBajaDetalle,
    string? MotivoRechazo,
    DateTimeOffset? SincronizadoEn,
    string? SincronizadoPor,
    IReadOnlyList<string> Acciones);

public sealed record DocenteConciliacionDto(
    Guid PersonaId,
    string Nombre,
    string Apellido,
    string? Cuil,
    string Documento,
    bool PersonaNueva,
    PedidoConciliacionDto Pedido,
    IReadOnlyList<DesignacionVigenteDto> Vigentes);

public sealed record ConciliacionDto(PeriodoDto? Periodo, IReadOnlyList<DocenteConciliacionDto> Docentes);

public sealed record PedidoAceptadoDto(Guid Id, uint Version);

public sealed record SincronizarConciliacionDto(IReadOnlyList<PedidoAceptadoDto> Pedidos);

public sealed record ResultadoSincronizacionDto(IReadOnlyList<string> Numeros);
