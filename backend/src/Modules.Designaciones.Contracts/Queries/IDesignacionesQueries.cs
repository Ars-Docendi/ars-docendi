namespace Modules.Designaciones.Contracts.Queries;

/// <summary>
/// Un trámite ubicado: su número legible y con qué identificador se abre.
/// </summary>
/// <remarks>
/// <b>No lleva ruta.</b> A dónde va el usuario para verlo es una decisión de la
/// interfaz, y este contract lo consume el backend. Quien recibe esto sabe QUÉ es;
/// DÓNDE se muestra lo resuelve el cliente.
/// </remarks>
public sealed record PedidoUbicableDto(string Numero, Guid Id);

/// <summary>
/// Los datos clave de un trámite, para mostrarlo en una ficha.
/// </summary>
/// <param name="Id">Con qué identificador se abre el trámite.</param>
/// <param name="Numero">Número legible, el que ve el usuario.</param>
/// <param name="Novedad">Qué pide el trámite: alta, baja o cambio.</param>
/// <param name="Materia">Nombre de la materia de la cátedra.</param>
/// <param name="Estado">Código del estado, estable; el cliente decide cómo pintarlo.</param>
/// <param name="EstadoEtiqueta">
/// Cómo se llama el estado para una persona. La pone este módulo porque el
/// vocabulario del circuito es suyo.
/// </param>
/// <param name="Periodo">Nombre del período del trámite.</param>
/// <param name="Cargo">Cargo solicitado, si el trámite lo tiene.</param>
/// <param name="Dedicacion">Dedicación solicitada, si el trámite la tiene.</param>
/// <param name="UltimaActividad">
/// Fecha del último movimiento del historial; si todavía no hay ninguno, la de
/// creación.
/// </param>
/// <remarks>
/// <b>No lleva ruta ni etiquetas de campo:</b> el módulo dice QUÉ es el trámite y la
/// interfaz decide cómo se ve y a dónde lleva.
/// </remarks>
public sealed record FichaDePedidoDto(
    Guid Id,
    string Numero,
    string Novedad,
    string Materia,
    string Estado,
    string EstadoEtiqueta,
    string Periodo,
    string? Cargo,
    string? Dedicacion,
    DateTimeOffset UltimaActividad);

/// <summary>
/// Consultas públicas del módulo de designaciones para otros módulos.
/// </summary>
public interface IDesignacionesQueries
{
    /// <summary>
    /// De los textos dados, los que son número de un trámite que el actor
    /// autenticado <b>puede abrir</b>.
    /// </summary>
    /// <remarks>
    /// <b>Por qué existe.</b> Quien pregunta —hoy el asistente conversacional—
    /// consiguió esos textos por otro camino y con otras reglas: la RLS del
    /// asistente decide qué FILAS ve una consulta generada, y el detalle del
    /// trámite lo autoriza este módulo. No son la misma regla y ya divergen: el
    /// ámbito departamental acá es una lista fija de códigos de rol y allá es
    /// <c>identity.roles.scope</c>; los roles de acá salen del token y están
    /// filtrados por el rol seleccionado en la sesión, y los de allá son las
    /// asignaciones vigentes leídas en vivo.
    ///
    /// Ofrecer una pantalla porque «la otra regla dejó pasar la fila» produce
    /// exactamente el botón que responde 403 al apretarlo. Por eso la pregunta se
    /// hace acá, contra el mismo criterio que <c>GET /api/designaciones/pedidos/{id}</c>.
    ///
    /// <b>Devuelve menos de lo que se pide, nunca más.</b> Un texto que no tiene
    /// forma de número de trámite se descarta sin consultar la base; uno que no
    /// existe, o que existe fuera del ámbito del actor, no vuelve. Quien llama no
    /// necesita saber cuál de las tres cosas pasó, y decírselo sería un canal de
    /// inferencia sobre trámites que no puede ver.
    /// </remarks>
    Task<IReadOnlyList<PedidoUbicableDto>> UbicarPedidosAsync(
        IReadOnlyCollection<string> textos, CancellationToken ct);

    /// <summary>
    /// La ficha de un trámite, o nada si el actor autenticado no puede abrirlo.
    /// </summary>
    /// <remarks>
    /// <b>Mismo criterio que el detalle.</b> Se filtra con el ámbito de
    /// <c>GET /api/designaciones/pedidos/{id}</c>, para que ofrecer la ficha y abrir
    /// el detalle nunca discrepen: una ficha cuyo botón termina en 403 es lo que
    /// <see cref="UbicarPedidosAsync"/> ya evita para los vínculos.
    ///
    /// <b>Devuelve <c>null</c> igual</b> para un trámite inexistente y para uno fuera
    /// del ámbito: distinguirlos sería un canal de inferencia sobre trámites ajenos.
    /// La política de permiso <c>designaciones.ver</c> es de quien llama, como en el
    /// endpoint.
    /// </remarks>
    Task<FichaDePedidoDto?> FichaDePedidoAsync(Guid id, CancellationToken ct);
}
