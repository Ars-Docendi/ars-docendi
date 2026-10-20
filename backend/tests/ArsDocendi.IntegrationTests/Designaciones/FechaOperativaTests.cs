using System.Globalization;
using Modules.Designaciones.Domain;

namespace ArsDocendi.IntegrationTests.Designaciones;

public sealed class FechaOperativaTests
{
    [Theory]
    [InlineData("2026-10-20T00:30:00Z", "2026-10-19")]
    [InlineData("2026-10-20T02:59:59Z", "2026-10-19")]
    [InlineData("2026-10-20T03:00:00Z", "2026-10-20")]
    public void El_dia_operativo_sigue_el_calendario_argentino(string utc, string esperado)
    {
        var reloj = new RelojFijo(DateTimeOffset.Parse(utc, CultureInfo.InvariantCulture));

        Assert.Equal(DateOnly.Parse(esperado, CultureInfo.InvariantCulture), FechaOperativa.Hoy(reloj));
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }
}
