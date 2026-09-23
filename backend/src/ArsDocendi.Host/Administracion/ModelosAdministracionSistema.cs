namespace ArsDocendi.Host.Administracion;

public sealed record EstadoBaseDatosDto(
    string Estado,
    DateTimeOffset ComprobadoEn,
    double DuracionMs);

public sealed record ConsultaAuditoriaDto(
    DateTimeOffset? Desde = null,
    DateTimeOffset? Hasta = null,
    string? Accion = null,
    string? Schema = null,
    string? Tabla = null,
    Guid? CambiadoPor = null,
    string? RowPk = null,
    int Pagina = 1,
    int TamanoPagina = 50);

public sealed record PaginaAuditoriaDto(
    IReadOnlyList<EventoAuditoriaDto> Elementos,
    int Pagina,
    int TamanoPagina,
    long Total);

public sealed record EventoAuditoriaDto(
    long Id,
    string Schema,
    string Tabla,
    string RowPk,
    string Accion,
    DateTimeOffset CambiadoEn,
    Guid? CambiadoPor,
    string? RequestId,
    IReadOnlyList<string> ColumnasCambiadas,
    IReadOnlyList<CambioAuditoriaDto> Cambios);

public sealed record CambioAuditoriaDto(
    string Campo,
    string? ValorAnterior,
    string? ValorNuevo,
    bool Oculto);
