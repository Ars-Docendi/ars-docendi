using ArsDocendi.Host.Auditoria;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArsDocendi.IntegrationTests.Backend;

public sealed class JobsAuditoriaTests
{
    [Theory]
    [InlineData("--verificar-auditoria")]
    [InlineData("--verificar-restauracion")]
    [InlineData("--verificar-estado-auditoria")]
    [InlineData("--sondear-auditoria")]
    public async Task Configuracion_ausente_falla_cerrado_sin_exponer_secretos(string comando)
    {
        var resultado = await JobsVerificacionAuditoria.EjecutarAsync([comando],
            new ConfigurationBuilder().Build(), NullLogger.Instance, TestContext.Current.CancellationToken);
        Assert.Equal(2, resultado);
    }

    [Fact]
    public async Task Arranque_ordinario_no_ejecuta_jobs()
    {
        Assert.Null(await JobsVerificacionAuditoria.EjecutarAsync([], new ConfigurationBuilder().Build(),
            NullLogger.Instance, TestContext.Current.CancellationToken));
    }
}
