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

    [Fact]
    public async Task Local_provider_without_cassettes_states_that_values_are_not_masked()
    {
        var registro = await ArrancarAsync(new OpcionesAsistente
        {
            RedaccionSinEnmascarar = true,
            Proveedor = "local",
        });

        Assert.Contains(
            registro.DeNivel(LogLevel.Information),
            linea => linea.Contains("WITHOUT masking", StringComparison.Ordinal));
        Assert.Empty(registro.DeNivel(LogLevel.Warning));
    }

    [Theory]
    [InlineData("anthropic", "")]
    [InlineData("local", "cassettes")]
    public async Task Requested_but_not_in_effect_warns_and_keeps_masking(
        string provider, string cassetteDirectory)
    {
        var registro = await ArrancarAsync(new OpcionesAsistente
        {
            RedaccionSinEnmascarar = true,
            Proveedor = provider,
            DirectorioDeCassettes = cassetteDirectory,
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

    [Fact]
    public async Task The_log_never_carries_the_cassette_directory()
    {
        var registro = await ArrancarAsync(new OpcionesAsistente
        {
            RedaccionSinEnmascarar = true,
            Proveedor = "local",
            DirectorioDeCassettes = "/srv/secret-directory",
        });

        Assert.DoesNotContain("secret-directory", registro.Todo(), StringComparison.Ordinal);
    }
}
