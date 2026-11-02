using Modules.Designaciones.Domain;

namespace Modules.Designaciones.Services;

/// <summary>
/// Lo que un actor ve del avance de un pedido. Con <c>designaciones.ver_seguimiento</c> ve el
/// estado real, el historial y a quién se devolvió. Sin él (Jefe de Cátedra) ve un estado
/// simplificado: en proceso mientras circula, devuelto sólo si volvió a su rol (con el
/// motivo), y aprobado al terminar el circuito del departamento.
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

    public static VistaPedido Completa(Pedido pedido) => new(
        pedido.Estado,
        pedido.EtapaRetorno,
        pedido.PropietarioActual,
        pedido.Historial.ToArray(),
        pedido.Estado == EstadosPedido.Devuelto ? UltimoMotivo(pedido) : null);

    public static VistaPedido Simplificada(Pedido pedido)
    {
        var devueltoAlJefe = pedido.Estado == EstadosPedido.Devuelto
            && pedido.PropietarioActual == RolesCircuito.JefeCatedra;
        var estado = pedido.Estado switch
        {
            EstadosPedido.Borrador or EstadosPedido.Rechazado or EstadosPedido.Cancelado => pedido.Estado,
            EstadosPedido.EnLote => Aprobado,
            EstadosPedido.Devuelto when devueltoAlJefe => EstadosPedido.Devuelto,
            _ => EnProceso,
        };
        return new(
            estado,
            EtapaRetorno: null,
            PropietarioActual: devueltoAlJefe ? pedido.PropietarioActual : null,
            Historial: [],
            MotivoDevolucion: devueltoAlJefe ? UltimoMotivo(pedido) : null);
    }

    private static string? UltimoMotivo(Pedido pedido) =>
        pedido.Historial
            .Where(h => h.Accion == AccionesHistorial.Devolver)
            .OrderByDescending(h => h.CreadoEn)
            .ThenByDescending(h => h.Id)
            .FirstOrDefault()?.Comentario;
}
