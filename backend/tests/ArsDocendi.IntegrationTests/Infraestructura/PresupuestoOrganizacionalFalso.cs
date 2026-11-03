using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Infraestructura;

/// <summary>
/// Fake en memoria de <see cref="IPresupuestoOrganizacional"/>, para
/// <see cref="BancoDelAsistente"/> y cualquier test de
/// <c>CapaConversacional</c> que no esté ejercitando el tope organizacional.
/// </summary>
/// <remarks>
/// Sin tope real (<c>tope &lt;= 0</c>, el default), como si el Departamento
/// nunca lo hubiera configurado — el default en producción, per
/// design.md/Open Questions de asistente-administracion-de-uso.
/// </remarks>
internal sealed class PresupuestoOrganizacionalFalso(decimal tope = 0) : IPresupuestoOrganizacional
{
    public decimal Acumulado { get; private set; }

    public Task<bool> HayPresupuestoAsync(CancellationToken ct) =>
        Task.FromResult(tope <= 0 || Acumulado < tope);

    public Task AcumularAsync(
        string? proveedor,
        DateTimeOffset ocurridoEn,
        int tokensDeEntrada,
        int tokensDeSalida,
        int? tokensDeCache,
        CancellationToken ct)
    {
        // No necesita costear de verdad para lo que estos tests miden: alcanza
        // con anotar "un turno más" para que un tope configurado por el test
        // se pueda agotar contando llamadas.
        Acumulado += 1;
        return Task.CompletedTask;
    }
}
