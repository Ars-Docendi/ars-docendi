using Modules.Designaciones.Domain;

namespace Modules.Designaciones.Services;

/// <summary>
/// Lo que un actor ve del avance de un pedido. Con <c>designaciones.ver_seguimiento</c> ve el
/// estado real, el historial y a quién se devolvió. Sin él ve un estado simplificado: en
/// proceso mientras circula o espera la conciliación del lote, devuelto sólo si volvió a su
/// rol activo (con el motivo), aprobado una vez sincronizado y rechazado si lo rechazó el
/// circuito o la conciliación (en este caso, con el motivo de la conciliación).
/// <c>ultimoMotivo</c> es el comentario de la última devolución, que el listado lee aparte
/// porque su historial es un resumen.
/// </summary>
internal sealed record VistaPedido(
    string Estado,
    string? EtapaRetorno,
    string? PropietarioActual,
    IReadOnlyCollection<PedidoHistorial> Historial,
    string? MotivoDevolucion)
{
    /// <summary>Estados que sólo existen en la vista simplificada.</summary>
    public const string EnProceso = "en_proceso";
    public const string Aprobado = "aprobado";

    public static VistaPedido Completa(Pedido pedido, string? ultimoMotivo) => new(
        pedido.Estado,
        pedido.EtapaRetorno,
        pedido.PropietarioActual,
        pedido.Historial.ToArray(),
        pedido.Estado == EstadosPedido.Devuelto ? ultimoMotivo : null);

    public static VistaPedido Simplificada(Pedido pedido, ActorContexto actor, string? ultimoMotivo)
    {
        var devueltoAlActor = pedido.Estado == EstadosPedido.Devuelto
            && pedido.PropietarioActual is { } propietario
            && actor.Tiene(propietario);
        var estado = pedido.Estado switch
        {
            EstadosPedido.Borrador or EstadosPedido.Rechazado or EstadosPedido.Cancelado => pedido.Estado,
            EstadosPedido.Aprobado => Aprobado,
            EstadosPedido.NoAprobado => EstadosPedido.Rechazado,
            EstadosPedido.Devuelto when devueltoAlActor => EstadosPedido.Devuelto,
            _ => EnProceso,
        };
        var motivo = devueltoAlActor
            ? ultimoMotivo
            : pedido.Estado == EstadosPedido.NoAprobado
                ? pedido.Historial
                    .Where(h => h.Accion == AccionesHistorial.NoAprobar)
                    .MaxBy(h => h.CreadoEn)?.Comentario
                : null;
        return new(
            estado,
            EtapaRetorno: null,
            PropietarioActual: devueltoAlActor ? pedido.PropietarioActual : null,
            Historial: [],
            MotivoDevolucion: motivo);
    }

    public static string? UltimoMotivo(Pedido pedido) =>
        pedido.Historial
            .Where(h => h.Accion == AccionesHistorial.Devolver)
            .OrderByDescending(h => h.CreadoEn)
            .ThenByDescending(h => h.Id)
            .FirstOrDefault()?.Comentario;
}
