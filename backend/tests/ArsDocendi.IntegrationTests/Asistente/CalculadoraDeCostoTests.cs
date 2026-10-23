using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// <see cref="CalculadoraDeCosto"/>: estimación de costo por fila, versionada
/// por precio vigente al momento en que la fila ocurrió (design.md D1/A1/D6
/// de asistente-administracion-de-uso).
/// </summary>
public sealed class CalculadoraDeCostoTests
{
    private static readonly DateTimeOffset Version1Desde = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CambioDePrecio = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Una_fila_usa_el_precio_vigente_en_su_propio_momento_no_el_mas_reciente()
    {
        var precios = new[]
        {
            new PrecioVigente(
                "anthropic", "claude-sonnet-5", 0.000_003m, 0.000_015m, 0.000_0003m,
                Version: 1, Version1Desde, CambioDePrecio),
            new PrecioVigente(
                "anthropic", "claude-sonnet-5", 0.000_006m, 0.000_030m, 0.000_0006m,
                Version: 2, CambioDePrecio, VigenteHasta: null),
        };

        var filaAntes = new FilaDeConsumo(
            "anthropic/claude-sonnet-5", new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            TokensDeEntrada: 1000, TokensDeSalida: 1000, TokensDeCache: null);

        var filaDespues = new FilaDeConsumo(
            "anthropic/claude-sonnet-5", new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            TokensDeEntrada: 1000, TokensDeSalida: 1000, TokensDeCache: null);

        var costoAntes = CalculadoraDeCosto.CostoDeUnaFila(filaAntes, precios);
        var costoDespues = CalculadoraDeCosto.CostoDeUnaFila(filaDespues, precios);

        Assert.Equal(0.000_003m * 1000 + 0.000_015m * 1000, costoAntes);
        Assert.Equal(0.000_006m * 1000 + 0.000_030m * 1000, costoDespues);

        // Un precio nuevo NO reescribe la estimación de una fila ya ocurrida.
        Assert.NotEqual(costoAntes, costoDespues);
    }

    [Fact]
    public void Los_tokens_de_cache_entran_al_costo_con_su_propio_precio()
    {
        var precios = new[]
        {
            new PrecioVigente(
                "anthropic", "claude-sonnet-5", 0.000_003m, 0.000_015m, 0.000_0003m,
                Version: 1, Version1Desde, VigenteHasta: null),
        };

        var fila = new FilaDeConsumo(
            "anthropic/claude-sonnet-5", Version1Desde.AddDays(1),
            TokensDeEntrada: 1000, TokensDeSalida: 0, TokensDeCache: 9000);

        var costo = CalculadoraDeCosto.CostoDeUnaFila(fila, precios);

        Assert.Equal((0.000_003m * 1000) + (0.000_0003m * 9000), costo);
    }

    [Fact]
    public void Una_fila_sin_precio_para_su_proveedor_no_se_costea_en_cero()
    {
        var precios = new[]
        {
            new PrecioVigente(
                "anthropic", "claude-sonnet-5", 0.000_003m, 0.000_015m, 0.000_0003m,
                Version: 1, Version1Desde, VigenteHasta: null),
        };

        var filaSinPrecio = new FilaDeConsumo(
            "local/llama-desconocido", Version1Desde.AddDays(1),
            TokensDeEntrada: 1000, TokensDeSalida: 1000, TokensDeCache: null);

        Assert.Null(CalculadoraDeCosto.CostoDeUnaFila(filaSinPrecio, precios));

        var resultado = CalculadoraDeCosto.Costear(
            [
                filaSinPrecio,
                new FilaDeConsumo(
                    "anthropic/claude-sonnet-5", Version1Desde.AddDays(1), 1000, 1000, null),
            ],
            precios);

        Assert.Equal(1, resultado.FilasSinPrecio);
        Assert.Equal(0.000_003m * 1000 + 0.000_015m * 1000, resultado.CostoEstimadoTotal);
    }

    [Fact]
    public void Una_fila_sin_proveedor_no_se_costea_en_cero()
    {
        var fila = new FilaDeConsumo(null, DateTimeOffset.UtcNow, 0, 0, null);

        Assert.Null(CalculadoraDeCosto.CostoDeUnaFila(fila, []));
    }

    [Fact]
    public void Una_fila_anterior_a_toda_version_de_precio_no_tiene_precio()
    {
        var precios = new[]
        {
            new PrecioVigente(
                "anthropic", "claude-sonnet-5", 0.000_003m, 0.000_015m, 0.000_0003m,
                Version: 1, CambioDePrecio, VigenteHasta: null),
        };

        var filaAntesDeExistirElPrecio = new FilaDeConsumo(
            "anthropic/claude-sonnet-5", Version1Desde, 1000, 1000, null);

        Assert.Null(CalculadoraDeCosto.CostoDeUnaFila(filaAntesDeExistirElPrecio, precios));
    }
}
