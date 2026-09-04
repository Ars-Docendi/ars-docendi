using Modules.Designaciones.Api;

namespace Modules.Designaciones.Services;

internal sealed record DatosPedido(
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
    uint? Version)
{
    public static DatosPedido Desde(GuardarPedidoDto datos) => new(
        datos.PeriodoId,
        datos.PersonaId,
        datos.MateriaId,
        datos.Novedad,
        datos.CargoSolicitadoId,
        datos.DedicacionSolicitada,
        datos.Horas,
        datos.HorasInvestigacion,
        datos.HorasExternas,
        datos.Justificacion,
        datos.TipoBaja,
        datos.TipoBajaDetalle,
        datos.Adjuntos,
        datos.Version);
}
