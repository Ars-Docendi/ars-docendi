using ArsDocendi.Shared.Identity;
using Modules.Designaciones.Domain;
using Modules.Notificaciones.Contracts;

namespace Modules.Designaciones.Services;

/// <summary>
/// Notifications triggered by the review transitions of a designation request. The sentences are
/// the ones <c>ModalConfirmacionAccion</c> promises to the reviewer. Pure: it receives the already
/// resolved names and returns what to emit, or <c>null</c> when the transition notifies nobody.
/// </summary>
internal static class AvisosPedido
{
    private static readonly IReadOnlyDictionary<string, string> EtiquetaRol = new Dictionary<string, string>
    {
        [RolesCircuito.JefeCatedra] = "Jefe de Cátedra",
        [RolesCircuito.CoordinadorCarrera] = "Coordinador",
        [RolesCircuito.Secretaria] = "Secretaría",
        [RolesCircuito.Decanato] = "Decanato",
        [RolesCircuito.Administrativo] = "Administración",
    };

    private static readonly IReadOnlyDictionary<string, string> FraseNovedad = new Dictionary<string, string>
    {
        [Novedades.SinNovedad] = "la actualización del docente",
        [Novedades.CambioDeCargoODedicacion] = "el cambio de cargo o dedicación",
        [Novedades.Alta] = "el alta del docente",
        [Novedades.Baja] = "la baja del docente",
    };

    public sealed record Aviso(AvisoNuevo Contenido, DestinoNotificacion Destino);

    /// <param name="accion">The action that was applied.</param>
    /// <param name="pedido">The request AFTER the transition.</param>
    public static Aviso? Construir(AccionPedido accion, Pedido pedido, Persona? docente, string materia)
    {
        var sujeto = Sujeto(pedido, docente, materia);
        switch (accion)
        {
            case AccionPedido.Aceptar:
                // Entering en_lote has no reviewer role: nobody to tell.
                if (!RolesCircuito.RolPorEtapa.TryGetValue(pedido.Estado, out var rolDestino)) return null;
                return new Aviso(
                    Nuevo(TiposEventoNotificacion.PedidoParaRevisar, pedido, rolDestino, $"Pedido {pedido.Numero} para revisar",
                        $"Pasó a {EtiquetaRol[rolDestino]} {sujeto}. Esperando tu revisión.",
                        new() { ["numero"] = pedido.Numero, ["estado"] = pedido.Estado, ["etapaDestino"] = rolDestino }),
                    new DestinoNotificacion.RolEnAmbito(rolDestino));

            case AccionPedido.Rechazar rechazar:
                return new Aviso(
                    Nuevo(TiposEventoNotificacion.PedidoRechazado, pedido, RolesCircuito.JefeCatedra, $"Pedido {pedido.Numero} rechazado",
                        $"Se rechazó {sujeto}. Justificativo: {rechazar.Justificativo}",
                        new() { ["numero"] = pedido.Numero, ["justificativo"] = rechazar.Justificativo }),
                    new DestinoNotificacion.RolEnAmbito(RolesCircuito.JefeCatedra, MateriaId: pedido.MateriaId));

            case AccionPedido.Devolver devolver when pedido.PropietarioActual is { } propietario:
                // The owner of the returned request is the role one level back [BR-designaciones-014]:
                // Jefe by materia, Coordinador by carrera, Secretaría for the whole department.
                var destino = propietario switch
                {
                    RolesCircuito.JefeCatedra => new DestinoNotificacion.RolEnAmbito(propietario, MateriaId: pedido.MateriaId),
                    RolesCircuito.CoordinadorCarrera => new DestinoNotificacion.RolEnAmbito(propietario, CarreraId: pedido.CarreraId),
                    _ => new DestinoNotificacion.RolEnAmbito(propietario),
                };
                var etiqueta = EtiquetaRol.TryGetValue(propietario, out var nombre) ? nombre : "el propietario";
                return new Aviso(
                    Nuevo(TiposEventoNotificacion.PedidoDevuelto, pedido, propietario, $"Pedido {pedido.Numero} devuelto",
                        $"Se devolvió {sujeto} para que lo corrija {etiqueta}. Comentario: {devolver.Comentario}",
                        new() { ["numero"] = pedido.Numero, ["comentario"] = devolver.Comentario, ["propietario"] = propietario }),
                    destino);

            default:
                return null;
        }
    }

    private static string Sujeto(Pedido pedido, Persona? docente, string materia)
    {
        var nombre = docente is null ? "docente" : $"{docente.Apellido}, {docente.Nombre}";
        var frase = FraseNovedad.GetValueOrDefault(pedido.Novedad, "la novedad del docente");
        return $"el pedido {pedido.Numero} ({frase} de Prof. {nombre} · {materia})";
    }

    private static AvisoNuevo Nuevo(
        string tipoEvento, Pedido pedido, string rolDestino, string titulo, string mensaje, Dictionary<string, object?> contexto) =>
        new(tipoEvento, TiposEntidadNotificacion.PedidoDesignacion, pedido.Id, rolDestino, titulo, mensaje, contexto);
}
