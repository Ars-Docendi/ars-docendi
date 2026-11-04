namespace Modules.Notificaciones.Domain;

/// <summary>
/// One notification for one recipient user. The text is a snapshot written at emission time and
/// never changes; after insert only <see cref="LeidaEn"/> is set. It is never deleted.
/// </summary>
public sealed class Notificacion
{
    public Guid Id { get; set; }
    public Guid DestinatarioUsuarioId { get; set; }
    /// <summary>Profile under which it is visible; <c>null</c> means any profile.</summary>
    public string? RolDestino { get; set; }
    public required string TipoEvento { get; set; }
    public required string TipoEntidad { get; set; }
    public Guid EntidadId { get; set; }
    public required string Titulo { get; set; }
    public required string Mensaje { get; set; }
    /// <summary>JSON object (jsonb) with the structured data the text was built from.</summary>
    public required string Contexto { get; set; }
    public DateTimeOffset? LeidaEn { get; set; }
    public DateTimeOffset CreadaEn { get; set; }
}
