using System.Text.Json;
using Modules.Notificaciones.Contracts;

namespace Modules.Notificaciones.Services;

/// <summary>One recipient of a notice and the id of the in-app row created for them.</summary>
public sealed record DestinoPush(Guid UsuarioId, Guid NotificacionId);

/// <summary>A notice to push to its recipients. Content included: it never reaches a log.</summary>
public sealed record TrabajoPush(
    string TipoEvento, string TipoEntidad, Guid EntidadId, string Titulo, string Mensaje,
    IReadOnlyList<DestinoPush> Destinos);

/// <summary>
/// The JSON the service worker receives: <c>{ titulo, mensaje, url, id }</c>. Web Push encrypts it
/// end to end (RFC 8291), so the push services only carry ciphertext. The size limit of a payload is
/// about 4 KB, hence the truncation.
/// </summary>
public static class CargaPush
{
    public const int MaximoTitulo = 80;
    public const int MaximoMensaje = 240;

    public static string Serializar(TrabajoPush trabajo, Guid notificacionId) =>
        JsonSerializer.Serialize(new
        {
            titulo = Recortar(trabajo.Titulo, MaximoTitulo),
            mensaje = Recortar(trabajo.Mensaje, MaximoMensaje),
            url = RutaDeEntidad(trabajo.TipoEntidad, trabajo.EntidadId),
            id = notificacionId,
        });

    /// <summary>The same routes the in-app panel navigates to (<c>rutaDeEntidad</c> in the frontend).</summary>
    public static string RutaDeEntidad(string tipoEntidad, Guid entidadId) => tipoEntidad switch
    {
        TiposEntidadNotificacion.SolicitudAula => $"/aulas/solicitudes/{entidadId}",
        TiposEntidadNotificacion.PedidoDesignacion => $"/designaciones/pedidos/{entidadId}",
        TiposEntidadNotificacion.Tarea => $"/tareas/{entidadId}",
        _ => "/",
    };

    private static string Recortar(string texto, int maximo) =>
        texto.Length > maximo ? texto[..(maximo - 1)].TrimEnd() + "…" : texto;
}
