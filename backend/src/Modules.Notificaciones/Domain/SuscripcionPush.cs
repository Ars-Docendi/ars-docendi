namespace Modules.Notificaciones.Domain;

/// <summary>One Web Push subscription (browser/device). The endpoint is unique and belongs to the user who subscribed it last.</summary>
public sealed class SuscripcionPush
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public required string Endpoint { get; set; }
    public required string P256dh { get; set; }
    public required string Auth { get; set; }
    public DateTimeOffset CreadaEn { get; set; }
}
