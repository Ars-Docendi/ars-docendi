using System.Text.Json;

namespace Modules.Notificaciones.Api;

public sealed record CantidadNoLeidasDto(int Cantidad);

public sealed record CantidadActualizadaDto(int CantidadActualizada);

public sealed record NotificacionDto(
    Guid Id,
    string? RolDestino,
    string TipoEvento,
    string TipoEntidad,
    Guid EntidadId,
    string Titulo,
    string Mensaje,
    JsonElement Contexto,
    DateTimeOffset? LeidaEn,
    DateTimeOffset CreadaEn);

public sealed record PaginaNotificacionesDto(
    IReadOnlyList<NotificacionDto> Datos, int Total, int Pagina, int Tamanio, int TotalPaginas);
