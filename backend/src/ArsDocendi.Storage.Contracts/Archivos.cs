namespace ArsDocendi.Storage.Contracts;

public static class PropositosArchivo
{
    public const string Cv = "cv";
    public const string DniFrente = "dni_frente";
    public const string DniDorso = "dni_dorso";
    public const string Justificativo = "justificativo";
    public const string DocumentoProyecto = "documento_proyecto";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.Ordinal)
    {
        Cv, DniFrente, DniDorso, Justificativo, DocumentoProyecto,
    };

    public static bool EsImagen(string proposito) => proposito is DniFrente or DniDorso;
    public static bool EsPdf(string proposito) => proposito is Cv or Justificativo or DocumentoProyecto;
}

public static class EstadosArchivo
{
    public const string Pendiente = "pendiente";
    public const string Cuarentena = "cuarentena";
    public const string Disponible = "disponible";
    public const string Rechazado = "rechazado";
    public const string Eliminado = "eliminado";
}

public sealed record IniciarCargaArchivoDto(
    string Proposito,
    string NombreOriginal,
    string MimeDeclarado,
    long TamanoBytes);

public sealed record ConfirmarCargaArchivoDto(Guid ArchivoId, string? Sha256 = null, long? TamanoBytes = null);

public sealed record SesionCargaArchivoDto(
    Guid ArchivoId,
    string UrlSubida,
    DateTimeOffset ExpiraEn,
    string Metodo = "PUT");

public sealed record ArchivoDto(
    Guid Id,
    string Proposito,
    string NombreOriginal,
    string MimeDeclarado,
    string? MimeDetectado,
    long TamanoBytes,
    string? Sha256,
    string Estado,
    DateTimeOffset CreadoEn,
    DateTimeOffset? ConfirmadoEn,
    bool EsLegacy = false);

public sealed record DescargaArchivo(Stream Contenido, string Nombre, string Mime, long TamanoBytes);

public interface IAlmacenamientoArchivos
{
    Task<SesionCargaArchivoDto> IniciarCargaAsync(
        IniciarCargaArchivoDto datos,
        Guid propietarioId,
        CancellationToken ct);

    Task<ArchivoDto> ConfirmarCargaAsync(
        ConfirmarCargaArchivoDto datos,
        Guid propietarioId,
        CancellationToken ct);

    Task<ArchivoDto?> ObtenerAsync(Guid archivoId, CancellationToken ct);

    Task<bool> EsPropietarioAsync(Guid archivoId, Guid propietarioId, CancellationToken ct);

    Task<ArchivoDto> RequerirDisponibleDePropietarioAsync(
        Guid archivoId,
        string proposito,
        Guid propietarioId,
        CancellationToken ct);

    Task<DescargaArchivo?> AbrirDescargaAsync(Guid archivoId, CancellationToken ct);

    Task EliminarAsync(Guid archivoId, Guid propietarioId, CancellationToken ct);

    Task<int> LimpiarAsync(DateTimeOffset ahora, CancellationToken ct);
}
