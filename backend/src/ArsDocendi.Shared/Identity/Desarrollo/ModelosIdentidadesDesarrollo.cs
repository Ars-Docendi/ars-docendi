namespace ArsDocendi.Shared.Identity.Desarrollo;

/// <summary>Identidad sembrada que se puede elegir para ingresar en desarrollo.</summary>
public sealed record IdentidadDesarrolloDto(
    Guid UsuarioId,
    string NombreParaMostrar,
    string Upn,
    IReadOnlyList<string> Roles);
