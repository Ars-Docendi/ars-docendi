using ArsDocendi.Shared.Aplicacion;
using Modules.Designaciones.Domain;

namespace Modules.Designaciones.Services;

internal sealed partial class ServicioPedidos
{
    private async Task ValidarCompletitudCircuitoAsync(Pedido pedido, CancellationToken ct)
    {
        var errores = new Dictionary<string, string[]>();
        if (pedido.Novedad is Novedades.Alta or Novedades.CambioDeCargoODedicacion)
        {
            if (pedido.CargoSolicitadoId is not { } cargo || !await pedidos.ExisteCargoActivoAsync(cargo, ct))
                errores["cargoSolicitadoId"] = ["Seleccioná un cargo activo."];
            if (pedido.DedicacionSolicitadaId is not { } dedicacion || !await pedidos.ExisteDedicacionActivaAsync(dedicacion, ct))
                errores["dedicacionSolicitadaId"] = ["Seleccioná una dedicación activa."];
            if (pedido.Horas is null or <= 0)
                errores["horas"] = ["Las horas de materia deben ser mayores a cero."];
        }
        if (pedido.Novedad == Novedades.Alta)
        {
            var tipos = pedido.Adjuntos.Select(a => a.Tipo).ToHashSet();
            if (!new[] { TiposAdjunto.Cv, TiposAdjunto.DniFrente, TiposAdjunto.DniDorso }.All(tipos.Contains))
                errores["adjuntos"] = ["El Alta exige CV, DNI frente y DNI dorso [BR-designaciones-002]."];
        }
        if (pedido.Novedad == Novedades.CambioDeCargoODedicacion && string.IsNullOrWhiteSpace(pedido.Justificacion))
            errores["justificacion"] = ["El Cambio exige justificación [BR-designaciones-004]."];
        if (pedido.Novedad == Novedades.Baja && (string.IsNullOrWhiteSpace(pedido.TipoBaja)
            || !pedido.Adjuntos.Any(a => a.Tipo == TiposAdjunto.Justificativo)))
            errores["tipoBaja"] = ["La Baja exige tipo y justificativo [BR-designaciones-003]."];
        if (pedido.Novedad is Novedades.Baja or Novedades.CambioDeCargoODedicacion)
        {
            var persona = await identity.ObtenerPersonaAsync(pedido.PersonaId, ct);
            if (persona is null || string.IsNullOrWhiteSpace(persona.Legajo))
                errores["personaId"] = ["La Baja y el Cambio exigen legajo [BR-designaciones-018]."];
            await ValidarDesignacionVigenteAsync(pedido, ct);
        }
        if (errores.Count > 0)
            throw new ExcepcionAplicacion(TipoErrorAplicacion.Validacion, "validation",
                "Completá los datos del pedido antes de continuar el circuito.", errores);
    }
}
