using Microsoft.Extensions.Logging;
using Modules.Asistente;
using ArsDocendi.IntegrationTests.Infraestructura;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// The startup log declares whether redaction masks <c>sensible-valor</c> values,
/// and warns when the option is requested but not in effect.
/// </summary>
public sealed class AnuncioDeLaRedaccionTests
{
    private static async Task<RegistroDeCapturas> ArrancarAsync(OpcionesAsistente options)
    {
        var registro = new RegistroDeCapturas();

        await new AnuncioDeLaRedaccion(options, registro.Logger<AnuncioDeLaRedaccion>())
            .StartAsync(TestContext.Current.CancellationToken);

        return registro;
    }

    [Fact]
    public async Task Default_configuration_states_that_values_are_masked_and_does_not_warn()
    {
        var registro = await ArrancarAsync(new OpcionesAsistente());

        Assert.Contains(
            registro.DeNivel(LogLevel.Information),
            linea => linea.Contains("masks", StringComparison.Ordinal));
        Assert.Empty(registro.DeNivel(LogLevel.Warning));
    }

    [Theory]
    [InlineData("local")]
    [InlineData("ollama")]
    public async Task Own_hardware_provider_states_that_values_are_not_masked(
        string provider)
    {
        var registro = await ArrancarAsync(new OpcionesAsistente
        {
            RedaccionSinEnmascarar = true,
            Proveedor = provider,
        });

        Assert.Contains(
            registro.DeNivel(LogLevel.Information),
            linea => linea.Contains("WITHOUT masking", StringComparison.Ordinal));
        Assert.Empty(registro.DeNivel(LogLevel.Warning));
    }

    [Fact]
    public async Task Requested_but_not_in_effect_warns_and_keeps_masking()
    {
        var registro = await ArrancarAsync(new OpcionesAsistente
        {
            RedaccionSinEnmascarar = true,
            Proveedor = "anthropic",
        });

        Assert.Contains(
            registro.DeNivel(LogLevel.Warning),
            linea => linea.Contains("not in effect", StringComparison.Ordinal));
        Assert.Contains(
            registro.DeNivel(LogLevel.Information),
            linea => linea.Contains("masks", StringComparison.Ordinal));
        Assert.DoesNotContain(
            registro.DeNivel(LogLevel.Information),
            linea => linea.Contains("WITHOUT masking", StringComparison.Ordinal));
    }
}
