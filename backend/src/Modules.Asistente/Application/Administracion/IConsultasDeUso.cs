namespace Modules.Asistente.Application;

/// <summary>El período sobre el que se agrega el panel de uso.</summary>
public sealed record RangoDePeriodo(DateTimeOffset Desde, DateTimeOffset Hasta);

/// <summary>
/// Un agregado de uso: por usuario, por rol, u organizacional
/// (asistente-panel-de-uso).
/// </summary>
/// <param name="Clave">
/// El id del actor (como texto), el código del rol, o <c>"organizacion"</c>.
/// </param>
/// <param name="NombreParaMostrar">
/// Sólo para agregados por usuario, resuelto vía
/// <see cref="ArsDocendi.Shared.Identity.IConsultasIdentity.ListarUsuariosAsync"/>
/// (design.md D12) — nunca vía <c>usuarios.ver</c>.
/// </param>
/// <param name="CostoEstimado">
/// Suma de <see cref="CalculadoraDeCosto"/> sobre las filas CON precio. Nunca
/// incluye las filas de <paramref name="TurnosSinPrecio"/>.
/// </param>
/// <param name="TurnosSinPrecio">
/// Turnos cuyo proveedor/modelo no tenía ningún precio vigente al momento en
/// que ocurrieron — reportados aparte, nunca costeados en cero.
/// </param>
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
    int TurnosSinPrecio);

/// <summary>El panel de uso completo para un período (asistente-panel-de-uso).</summary>
public sealed record PanelDeUso(
    IReadOnlyList<UsoAgregado> PorUsuario,
    IReadOnlyList<UsoAgregado> PorRol,
    UsoAgregado Organizacion);

/// <summary>
/// Agrega <c>asistente.registro_operativo</c> para el panel de uso
/// (asistente-panel-de-uso, gated por <c>asistente.administrar</c>).
/// </summary>
/// <remarks>
/// Sólo <c>registro_operativo</c>, nunca <c>registro_analitico</c> — el
/// primero no tiene el texto de las preguntas, y por eso puede tener actor;
/// el segundo tiene el texto y por eso nunca lleva actor (TD-012). Cruzarlos
/// acá reabriría exactamente el canal que esa separación cierra.
/// </remarks>
public interface IConsultasDeUso
{
    Task<PanelDeUso> ObtenerAsync(RangoDePeriodo periodo, CancellationToken ct);
}
