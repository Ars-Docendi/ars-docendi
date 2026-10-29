using Microsoft.Extensions.Logging;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure;
using Modules.Designaciones.Repositories;

namespace Modules.Designaciones.Services.Integridad;

/// <summary>
/// Agrega el eslabón de cada evento del circuito a la cadena de su período
/// [BR-designaciones-019]. Corre dentro de la transacción del evento: si el sello falla,
/// el evento tampoco se confirma.
/// </summary>
internal sealed class SelladorPedidos(
    RepositorioSellos sellos,
    ClavesSellado claves,
    UnidadDeTrabajo unidadDeTrabajo,
    ILogger<SelladorPedidos> logger)
{
    /// <summary>
    /// Sella el evento ya agregado al pedido, con el pedido en su estado posterior. Un
    /// borrador es privado del Jefe de Cátedra y la cadena empieza al enviarlo.
    /// </summary>
    public async Task SellarAsync(Pedido pedido, PedidoHistorial evento, CancellationToken ct)
    {
        if (evento.Etapa == EstadosPedido.Borrador) return;
        await sellos.BloquearCadenaAsync(pedido.PeriodoId, ct);
        var cabeza = await sellos.ObtenerCabezaAsync(pedido.PeriodoId, ct);
        sellos.Agregar(Crear(pedido, evento, cabeza));
    }

    /// <summary>
    /// Sella, período por período, los eventos sin sello de pedidos que ya salieron de
    /// borrador. Los eventos previos al despliegue no conservan el estado de su momento:
    /// todos fotografían el estado actual, que es lo que el último sello debe proteger.
    /// </summary>
    public async Task<int> SellarHistorialExistenteAsync(CancellationToken ct)
    {
        var total = 0;
        foreach (var periodoId in await sellos.ListarPeriodosAsync(ct))
        {
            await unidadDeTrabajo.EjecutarEnTransaccionAsync(async token =>
            {
                await sellos.BloquearCadenaAsync(periodoId, token);
                var pedidos = await sellos.ListarPedidosEnviadosAsync(periodoId, token);
                var eventos = pedidos.SelectMany(p => p.Historial).ToList();
                var sellados = await sellos.ListarEventosSelladosAsync(eventos.Select(e => e.Id).ToList(), token);
                var pendientes = pedidos
                    .SelectMany(p => EventosRetroactivos(p, sellados).Select(e => (Pedido: p, Evento: e)))
                    .OrderBy(x => x.Evento.CreadoEn).ThenBy(x => x.Evento.Id)
                    .ToList();
                if (pendientes.Count == 0) return;

                PedidoSello? cabeza = await sellos.ObtenerCabezaAsync(periodoId, token);
                foreach (var (pedido, evento) in pendientes)
                {
                    cabeza = Crear(pedido, evento, cabeza);
                    sellos.Agregar(cabeza);
                }
                sellos.AgregarAncla(new PeriodoAncla
                {
                    Id = Guid.NewGuid(),
                    PeriodoId = periodoId,
                    Secuencia = cabeza!.Secuencia,
                    Hash = cabeza.Hash,
                    Motivo = MotivosAncla.Retroactivo,
                    CreadoEn = DateTimeOffset.UtcNow,
                });
                await sellos.GuardarAsync(token);
                total += pendientes.Count;
                logger.LogInformation(
                    "Período {PeriodoId}: {Cantidad} eventos sellados retroactivamente", periodoId, pendientes.Count);
            }, ct);
        }
        return total;
    }

    /// <summary>Lo que el verificador exige sellado: todo evento fuera de borrador.</summary>
    private static IEnumerable<PedidoHistorial> EventosRetroactivos(Pedido pedido, IReadOnlySet<Guid> sellados) =>
        pedido.Historial.Where(e => e.Etapa != EstadosPedido.Borrador && !sellados.Contains(e.Id));

    private PedidoSello Crear(Pedido pedido, PedidoHistorial evento, PedidoSello? cabeza)
    {
        var secuencia = (cabeza?.Secuencia ?? 0) + 1;
        var anterior = cabeza?.Hash ?? CanonicoSellos.Genesis(pedido.PeriodoId);
        var hashEstado = CanonicoSellos.HashEstado(pedido);
        var hash = CanonicoSellos.Hash(anterior, pedido.PeriodoId, secuencia, evento, hashEstado);
        return new PedidoSello
        {
            PeriodoId = pedido.PeriodoId,
            Secuencia = secuencia,
            HistorialId = evento.Id,
            PedidoId = pedido.Id,
            HashAnterior = anterior,
            HashEstado = hashEstado,
            Hash = hash,
            Firma = claves.Firmar(hash),
            VersionClave = claves.VersionVigente,
            CreadoEn = DateTimeOffset.UtcNow,
        };
    }
}
