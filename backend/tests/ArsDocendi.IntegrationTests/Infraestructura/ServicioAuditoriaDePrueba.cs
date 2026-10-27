using ArsDocendi.Host.Administracion;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Asistente.Contracts;

namespace ArsDocendi.IntegrationTests.Infraestructura;

/// <summary>
/// <see cref="ServicioAuditoria"/> sólo con la fuente <c>audit.change_log</c>: el
/// rastro de administración del asistente llega vacío.
/// </summary>
internal static class ServicioAuditoriaDePrueba
{
    public static ServicioAuditoria Crear(IRepositorioAuditoria repositorio) =>
        new(repositorio,
            new FuenteAuditoriaAsistente(new SinAuditoriaDeAdministracion(), repositorio),
            NullLogger<ServicioAuditoria>.Instance);

    private sealed class SinAuditoriaDeAdministracion : IConsultasDeAuditoriaDeAdministracion
    {
        public Task<LoteDeAuditoriaDeAdministracion> ListarAsync(
            DateTimeOffset? desde, DateTimeOffset? hasta, CancellationToken ct) =>
            Task.FromResult(new LoteDeAuditoriaDeAdministracion([], false));
    }
}
