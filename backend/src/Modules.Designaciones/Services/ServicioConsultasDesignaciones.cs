using System.Text.RegularExpressions;
using ArsDocendi.Shared.Identity;
using Modules.Designaciones.Contracts.Queries;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Repositories;

namespace Modules.Designaciones.Services;

/// <inheritdoc cref="IDesignacionesQueries" />
internal sealed partial class ServicioConsultasDesignaciones(
    RepositorioPedidos repositorio,
    ResolutorActor resolutorActor,
    IConsultasIdentity identity) : IDesignacionesQueries
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
                .Where(p => MaquinaEstadosPedido.AlcanzaAmbito(new AlcancePedido(p.MateriaId, p.CarreraId), actor))
                .Select(p => new PedidoUbicableDto(p.Numero, p.Id)),
        ];
    }

    public async Task<FichaDePedidoDto?> FichaDePedidoAsync(Guid id, CancellationToken ct)
    {
        var datos = await repositorio.ObtenerFichaAsync(id, ct);

        // Inexistente y fuera de ámbito se ven igual desde afuera: ninguno vuelve.
        if (datos is null)
        {
            return null;
        }

        var actor = await resolutorActor.ResolverAsync(ct);

        if (!MaquinaEstadosPedido.AlcanzaAmbito(new AlcancePedido(datos.MateriaId, datos.CarreraId), actor))
        {
            return null;
        }

        // Mismo criterio que el detalle (ServicioPedidosApi): la materia sale del
        // catálogo de identity. Un trámite cuya materia ya no está no se mapea
        // allá, así que tampoco hay ficha acá.
        var materia = (await identity.ListarMateriasAsync(ct)).FirstOrDefault(m => m.Id == datos.MateriaId);

        if (materia is null)
        {
            return null;
        }

        return new FichaDePedidoDto(
            datos.Id,
            datos.Numero,
            datos.Novedad,
            materia.Nombre,
            datos.Estado,
            EtiquetaDelEstado(datos.Estado),
            datos.Periodo,
            datos.Cargo,
            datos.Dedicacion,
            datos.UltimoMovimiento ?? datos.CreadoEn);
    }

    /// <summary>
    /// Cómo se llama cada estado del circuito para una persona.
    /// </summary>
    /// <remarks>
    /// El vocabulario es el de la interfaz de designaciones (<c>EstadoPedidoBadge</c>),
    /// para que la ficha y la pantalla del trámite digan lo mismo. Un código que no
    /// está acá se devuelve tal cual: mostrar el código es mejor que inventar una
    /// palabra.
    /// </remarks>
    private static string EtiquetaDelEstado(string estado) => estado switch
    {
        EstadosPedido.Borrador => "Borrador",
        EstadosPedido.EnRevisionCoordinador => "En Coordinación",
        EstadosPedido.EnRevisionSecretaria => "En Secretaría",
        EstadosPedido.EnRevisionDecanato => "En Decanato",
        EstadosPedido.Devuelto => "Devuelto",
        EstadosPedido.EnLote => "En lote",
        EstadosPedido.Rechazado => "Rechazado",
        EstadosPedido.Cancelado => "Cancelado",
        _ => estado,
    };
}
