namespace Modules.Asistente.Application;

/// <summary>El período sobre el que se agrega el panel de uso.</summary>
public sealed record RangoDePeriodo(DateTimeOffset Desde, DateTimeOffset Hasta);

/// <summary>
/// Un agregado de uso: por usuario, por rol, u organizacional (asistente-panel-de-uso).
/// </summary>
public sealed record UsoAgregado(
    string Clave,
    string? NombreParaMostrar,
    int Turnos,
    IReadOnlyDictionary<string, int> PorEstado,
    int LlamadasAlModelo,
    long TokensDeEntrada,
    long TokensDeSalida,
    long TokensDeCache,
    double LatenciaPromedioMs,
    double LatenciaP95Ms,
    IReadOnlyList<string> Proveedores,
    decimal CostoEstimado,
    int TurnosSinPrecio,
    IReadOnlyList<string> CodigosDeRol);

/// <summary>El panel de uso completo para un período (asistente-panel-de-uso).</summary>
public sealed record PanelDeUso(
    IReadOnlyList<UsoAgregado> PorUsuario,
    IReadOnlyList<UsoAgregado> PorRol,
    UsoAgregado Organizacion);

/// <summary>
/// Agrega <c>asistente.registro_operativo</c> para el panel de uso (asistente-panel-de-uso, gated
/// por <c>asistente.administrar</c>).
/// </summary>
public interface IConsultasDeUso
{
    Task<PanelDeUso> ObtenerAsync(RangoDePeriodo periodo, CancellationToken ct);
}
