using Modules.Designaciones.Domain;

namespace Modules.Designaciones.Api;

public sealed record OpcionPedidoDto(Guid Id, string Codigo, string Nombre);

public sealed record PersonaPedidoDto(
    Guid Id,
    string Nombre,
    string Apellido,
    string Documento,
    string? Legajo);

public sealed record MateriaPedidoDto(
    Guid Id,
    string Codigo,
    string Nombre,
    Guid CarreraId,
    string CarreraNombre);

public sealed record AdjuntoPedidoDto(Guid Id, string Tipo, string Nombre, string? Uri);
public sealed record GuardarAdjuntoPedidoDto(string Tipo, string Nombre, string? Uri = null);

public sealed record HistorialPedidoDto(
    Guid Id,
    string Accion,
    Guid RolId,
    string RolCodigo,
    Guid? ActorId,
    string? ActorNombre,
    string Etapa,
    string? Comentario,
    DateTimeOffset CreadoEn);

public sealed record PedidoDto(
    Guid Id,
    string Numero,
    PeriodoDto Periodo,
    PersonaPedidoDto Persona,
    MateriaPedidoDto Materia,
    string Novedad,
    string Estado,
    bool Prioritario,
    OpcionPedidoDto? CargoSolicitado,
    string? DedicacionSolicitada,
    int? Horas,
    int? HorasInvestigacion,
    int? HorasExternas,
    string? Justificacion,
    string? TipoBaja,
    string? TipoBajaDetalle,
    string? EtapaRetorno,
    string? PropietarioActual,
    SnapshotPedido? Snapshot,
    uint Version,
    IReadOnlyList<AdjuntoPedidoDto> Adjuntos,
    IReadOnlyList<HistorialPedidoDto> Historial,
    IReadOnlyList<string> AccionesPermitidas);

public sealed record GuardarPedidoDto(
    Guid PeriodoId,
    Guid PersonaId,
    Guid MateriaId,
    string Novedad,
    Guid? CargoSolicitadoId,
    string? DedicacionSolicitada,
    int? Horas,
    int? HorasInvestigacion,
    int? HorasExternas,
    string? Justificacion,
    string? TipoBaja,
    string? TipoBajaDetalle,
    IReadOnlyList<GuardarAdjuntoPedidoDto> Adjuntos,
    uint? Version = null);

public sealed record AccionPedidoDto(string? Comentario = null);
