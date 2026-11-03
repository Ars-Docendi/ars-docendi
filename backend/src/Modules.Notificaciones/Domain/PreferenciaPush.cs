namespace Modules.Notificaciones.Domain;

/// <summary>
/// Push switch of one event type for one user. Only the push channel: the in-app notice is always
/// created. A missing row means enabled.
/// </summary>
public sealed class PreferenciaPush
{
    public Guid UsuarioId { get; set; }
    public required string TipoEvento { get; set; }
    public bool Habilitado { get; set; }
    public DateTimeOffset ActualizadaEn { get; set; }
}
