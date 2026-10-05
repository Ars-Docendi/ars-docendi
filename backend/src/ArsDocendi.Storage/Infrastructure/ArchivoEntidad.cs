namespace ArsDocendi.Storage.Infrastructure;

public sealed class ArchivoEntidad
{
    public Guid Id { get; set; }
    public required string Proposito { get; set; }
    public required string Ambiente { get; set; }
    public required string Bucket { get; set; }
    public required string ClaveObjeto { get; set; }
    public required string NombreOriginal { get; set; }
    public required string MimeDeclarado { get; set; }
    public string? MimeDetectado { get; set; }
    public long TamanoBytes { get; set; }
    public string? Sha256 { get; set; }
    public required string Estado { get; set; }
    public Guid PropietarioId { get; set; }
    public DateTimeOffset ExpiraEn { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
    public DateTimeOffset? ConfirmadoEn { get; set; }
    public DateTimeOffset? RevisadoEn { get; set; }
    public string? MotivoRevision { get; set; }
    public DateTimeOffset? EliminadoEn { get; set; }
}
