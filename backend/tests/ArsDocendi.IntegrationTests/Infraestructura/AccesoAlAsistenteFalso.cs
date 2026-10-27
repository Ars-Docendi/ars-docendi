using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Infraestructura;

/// <summary>
/// <see cref="IAccesoAlAsistente"/> en memoria: por defecto todos tienen
/// acceso, que es lo que ve cualquier despliegue antes de que un
/// administrador apague un rol o revoque a alguien (asistente-acceso-granular).
/// </summary>
internal sealed class AccesoAlAsistenteFalso(bool tieneAcceso = true) : IAccesoAlAsistente
{
    public Task<bool> TieneAccesoAsync(Guid actor, CancellationToken ct) => Task.FromResult(tieneAcceso);
}
