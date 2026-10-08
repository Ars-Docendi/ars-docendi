using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Infraestructura;

/// <summary>
/// Fake en memoria de <see cref="IDisponibilidadDelModulo"/>, para
/// <see cref="BancoDelAsistente"/>.
/// </summary>
internal sealed class DisponibilidadDelModuloFalsa : IDisponibilidadDelModulo
{
    private EstadoDeMantenimiento _estado = new(false, null);

    public Task<EstadoDeMantenimiento> ConsultarAsync(CancellationToken ct) =>
        Task.FromResult(_estado);

    public Task ActivarAsync(Guid actor, string razon, CancellationToken ct)
    {
        _estado = new EstadoDeMantenimiento(true, razon);
        return Task.CompletedTask;
    }

    public Task DesactivarAsync(Guid actor, CancellationToken ct)
    {
        _estado = new EstadoDeMantenimiento(false, null);
        return Task.CompletedTask;
    }
}
