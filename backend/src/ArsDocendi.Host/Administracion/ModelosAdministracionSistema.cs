namespace ArsDocendi.Host.Administracion;

/// <summary>
/// Estado del backend y de PostgreSQL, más el modo mantenimiento del asistente
/// (sistema-seccion-unificada, design.md D7). Additive sobre el
/// <c>EstadoBaseDatosDto</c> original: los tres primeros campos conservan su
/// nombre.
/// </summary>
/// <param name="MantenimientoAsistente">
/// <c>activo</c>, <c>inactivo</c> o <c>desconocido</c> si la consulta falló o
/// venció su propio tiempo límite. Nunca contribuye al banner de salud cuando
/// es <c>desconocido</c> (design.md D7/D12).
/// </param>
public sealed record EstadoSistemaDto(
    string Estado,
    DateTimeOffset ComprobadoEn,
    double DuracionMs,
    string MantenimientoAsistente);

/// <summary>
/// Filtros de la consulta unificada (sistema-seccion-unificada, design.md D9).
/// <c>Modulo</c> reemplaza a <c>Schema</c> y admite además <c>"asistente"</c>;
/// <c>Q</c> reemplaza a <c>Actor</c> con alcance más amplio (label-only,
/// design.md D4). <c>CambiadoPor</c> se conserva por compatibilidad.
/// </summary>
public sealed record ConsultaAuditoriaDto(
    DateTimeOffset? Desde = null,
    DateTimeOffset? Hasta = null,
    string? Accion = null,
    string? Modulo = null,
    string? Tabla = null,
    Guid? CambiadoPor = null,
    string? RowPk = null,
    int Pagina = 1,
    int TamanoPagina = 50,
    string? Q = null);

/// <param name="Parcial">
/// <c>true</c> cuando alguna fuente falló y la página sólo trae las demás
/// (design.md D3). <c>Total</c> en ese caso cuenta sólo lo que sí se sirvió.
/// </param>
/// <param name="FuentesNoDisponibles">Los orígenes que fallaron (hoy sólo puede ser <c>["asistente"]</c>).</param>
public sealed record PaginaAuditoriaDto(
    IReadOnlyList<EventoAuditoriaDto> Elementos,
    int Pagina,
    int TamanoPagina,
    long Total,
    bool Parcial,
    IReadOnlyList<string> FuentesNoDisponibles);

/// <param name="Id">
/// <c>cambios-&lt;id&gt;</c> o <c>asistente-&lt;id&gt;</c> (design.md D3):
/// las dos fuentes comparten espacio de ids numéricos, así que el <c>Origen</c>
/// tiene que ir prefijado para que cada evento sea identificable sin ambigüedad.
/// </param>
/// <param name="Origen"><c>cambios</c> o <c>asistente</c> (design.md D3).</param>
public sealed record EventoAuditoriaDto(
    string Id,
    string Schema,
    string Tabla,
    string RowPk,
    string Accion,
    DateTimeOffset CambiadoEn,
    Guid? CambiadoPor,
    string? RequestId,
    IReadOnlyList<string> ColumnasCambiadas,
    IReadOnlyList<CambioAuditoriaDto> Cambios,
    string Actor,
    string AccionEtiqueta,
    string Modulo,
    string Objeto,
    string Resumen,
    /// <summary><c>persona</c> | <c>proceso</c> | <c>no_identificado</c> (design.md D6).</summary>
    string TipoActor,
    string Origen);

public sealed record CambioAuditoriaDto(
    string Campo,
    string? ValorAnterior,
    string? ValorNuevo,
    bool Oculto,
    string EtiquetaCampo);
