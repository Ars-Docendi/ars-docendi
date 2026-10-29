using ArsDocendi.Shared.Aplicacion;
using Modules.Designaciones.Api;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Repositories;

namespace Modules.Designaciones.Services.Integridad;

/// <summary>Categorías constantes; el id es técnico (sello→evento, pedido o ancla), nunca un valor de negocio.</summary>
public static class CategoriasIntegridad
{
    public const string SelloHashInvalido = "sello-hash-invalido";
    public const string SelloFirmaInvalida = "sello-firma-invalida";
    public const string SelloSecuenciaRota = "sello-secuencia-rota";
    public const string HistorialSinSello = "historial-sin-sello";
    public const string PedidoEstadoDivergente = "pedido-estado-divergente";
    public const string AnclaNoReproducida = "ancla-no-reproducida";
}

public sealed record HallazgoCadena(string Categoria, Guid Id);

public sealed record ResultadoIntegridad(
    bool Integro, long Secuencia, byte[] Hash, IReadOnlyList<HallazgoCadena> Hallazgos);

/// <summary>
/// Recalcula la cadena de un período y reporta lo que no reproduce [BR-designaciones-019].
/// Sólo lee: nunca corrige ni revela datos de negocio.
/// </summary>
public sealed class VerificadorIntegridadPedidos(RepositorioSellos sellos, ClavesSellado claves)
{
    public async Task<IntegridadPeriodoDto> ConsultarAsync(Guid periodoId, CancellationToken ct)
    {
        if (!await sellos.ExistePeriodoAsync(periodoId, ct))
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.NoEncontrado, "periodo-not-found", "No se encontró el período solicitado.");
        var resultado = await VerificarAsync(periodoId, ct);
        return new(resultado.Integro, resultado.Secuencia, Convert.ToHexStringLower(resultado.Hash),
            resultado.Hallazgos.Select(h => new HallazgoIntegridadDto(h.Categoria, h.Id)).ToList());
    }

    public async Task<ResultadoIntegridad> VerificarAsync(Guid periodoId, CancellationToken ct)
    {
        var cadena = await sellos.LeerCadenaAsync(periodoId, ct);
        var hallazgos = new List<HallazgoCadena>();
        var eventos = cadena.Eventos.ToDictionary(e => e.Id);
        var pedidos = cadena.Pedidos.ToDictionary(p => p.Id);

        var anterior = CanonicoSellos.Genesis(periodoId);
        var esperada = 1L;
        foreach (var sello in cadena.Sellos)
        {
            if (sello.Secuencia != esperada || !sello.HashAnterior.SequenceEqual(anterior))
                hallazgos.Add(new(CategoriasIntegridad.SelloSecuenciaRota, sello.HistorialId));
            if (!eventos.TryGetValue(sello.HistorialId, out var evento)
                || !CanonicoSellos.Hash(sello.HashAnterior, sello.PeriodoId, sello.Secuencia, evento, sello.HashEstado)
                    .SequenceEqual(sello.Hash))
                hallazgos.Add(new(CategoriasIntegridad.SelloHashInvalido, sello.HistorialId));
            if (claves.Firmar(sello.VersionClave, sello.Hash) is not { } firma || !firma.SequenceEqual(sello.Firma))
                hallazgos.Add(new(CategoriasIntegridad.SelloFirmaInvalida, sello.HistorialId));
            anterior = sello.Hash;
            esperada = sello.Secuencia + 1;
        }

        var sellosPorEvento = cadena.SellosDePedidos.ToDictionary(s => s.HistorialId);
        foreach (var pedido in pedidos.Values.Where(p => p.PeriodoId == periodoId))
        {
            var historial = cadena.Eventos.Where(e => e.PedidoId == pedido.Id).ToList();
            var primerSellado = historial.FindIndex(e => sellosPorEvento.ContainsKey(e.Id));
            // Fuera de borrador todo evento va sellado; tampoco puede aparecer un evento de
            // borrador después de que la cadena del pedido empezó.
            hallazgos.AddRange(historial
                .Where((e, i) => !sellosPorEvento.ContainsKey(e.Id)
                    && (e.Etapa != EstadosPedido.Borrador || (primerSellado >= 0 && i > primerSellado)))
                .Select(e => new HallazgoCadena(CategoriasIntegridad.HistorialSinSello, e.Id)));

            var ultimoSello = historial.LastOrDefault(e => sellosPorEvento.ContainsKey(e.Id)) is { } ultimo
                ? sellosPorEvento[ultimo.Id]
                : null;
            var diverge = ultimoSello is null
                ? pedido.Estado != EstadosPedido.Borrador
                : !CanonicoSellos.HashEstado(pedido).SequenceEqual(ultimoSello.HashEstado);
            if (diverge) hallazgos.Add(new(CategoriasIntegridad.PedidoEstadoDivergente, pedido.Id));
        }

        var porSecuencia = cadena.Sellos.ToDictionary(s => s.Secuencia, s => s.Hash);
        hallazgos.AddRange(cadena.Anclas
            .Where(a => !porSecuencia.TryGetValue(a.Secuencia, out var hash) || !hash.SequenceEqual(a.Hash))
            .Select(a => new HallazgoCadena(CategoriasIntegridad.AnclaNoReproducida, a.Id)));

        var cabeza = cadena.Sellos.LastOrDefault();
        var distintos = hallazgos.Distinct().ToList();
        return new(distintos.Count == 0, cabeza?.Secuencia ?? 0, cabeza?.Hash ?? CanonicoSellos.Genesis(periodoId), distintos);
    }
}
