using System.Text.RegularExpressions;
using Modules.Designaciones.Contracts.Queries;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Repositories;

namespace Modules.Designaciones.Services;

/// <inheritdoc cref="IDesignacionesQueries" />
internal sealed partial class ServicioConsultasDesignaciones(
    IRepositorioPedidos repositorio,
    ResolutorActor resolutorActor) : IDesignacionesQueries
{
    /// <summary>
    /// Forma del número de trámite: cuatro dígitos de año, guión, y la parte de la
    /// secuencia con al menos cuatro.
    /// </summary>
    /// <remarks>
    /// <b>El formato vive acá y no en quien pregunta.</b> Lo produce
    /// <c>designaciones.siguiente_numero_pedido()</c>, que es de este módulo; si el
    /// cliente pide numeración que reinicie por año, cambia esa función y esta
    /// expresión, y nadie más se entera. Un llamador que supiera la forma tendría
    /// que enterarse también.
    ///
    /// El <c>lpad</c> es a cuatro, no un tope: pasado el 9999 la secuencia sigue y
    /// el número crece, así que la expresión pide «cuatro o más» y no «exactamente
    /// cuatro».
    /// </remarks>
    [GeneratedRegex(@"^[0-9]{4}-[0-9]{4,}$")]
    private static partial Regex FormaDelNumero();

    /// <summary>
    /// Cuántos candidatos distintos se llevan a la base como mucho.
    /// </summary>
    /// <remarks>
    /// Quien pregunta manda todo lo que en su resultado tenía pinta de
    /// identificador, y ese resultado ya viene recortado del otro lado. El tope
    /// existe para que un llamador nuevo —o uno con un tope más alto— no convierta
    /// esta consulta en un <c>ANY</c> de miles de elementos sin que nadie lo note.
    /// </remarks>
    private const int TopeDeCandidatos = 100;

    public async Task<IReadOnlyList<PedidoUbicableDto>> UbicarPedidosAsync(
        IReadOnlyCollection<string> textos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(textos);

        var candidatos = textos
            .Where(texto => !string.IsNullOrWhiteSpace(texto) && FormaDelNumero().IsMatch(texto))
            .Distinct(StringComparer.Ordinal)
            .Take(TopeDeCandidatos)
            .ToArray();

        // Sin candidatos no se abre ninguna conexión. El caso mayoritario es una
        // respuesta que no cita ningún trámite, y ese caso no tiene que pagar nada.
        if (candidatos.Length == 0)
        {
            return [];
        }

        var pedidos = await repositorio.UbicarPorNumerosAsync(candidatos, ct);

        if (pedidos.Count == 0)
        {
            return [];
        }

        // El actor se resuelve DESPUÉS de saber que hay algo que filtrar: resolverlo
        // antes gastaría dos lecturas de identity en el caso en que no hay nada que
        // ubicar.
        var actor = await resolutorActor.ResolverAsync(ct);

        return
        [
            .. pedidos
                .Where(p => MaquinaEstadosPedido.AlcanzaAmbito(p.MateriaId, p.CarreraId, actor))
                .Select(p => new PedidoUbicableDto(p.Numero, p.Id)),
        ];
    }
}
