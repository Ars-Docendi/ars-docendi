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
}
