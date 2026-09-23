using System.Text.Json;
using ArsDocendi.Host.Administracion;
using ArsDocendi.Shared.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArsDocendi.IntegrationTests.Backend;

public sealed class RepositorioEstadoSistemaTests
{
    [Fact]
    public async Task Una_conexion_no_disponible_se_informa_sin_exponer_el_error_interno()
    {
        var opciones = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=unavailable;Username=test;Password=test;Timeout=1")
            .Options;
        await using var db = new IdentityDbContext(opciones);
        var servicio = new ServicioEstadoSistema(new RepositorioEstadoSistema(db));

        var estado = await servicio.ObtenerEstadoAsync(TestContext.Current.CancellationToken);
        var json = JsonSerializer.Serialize(estado);

        Assert.Equal("no_disponible", estado.Estado);
        Assert.True(estado.DuracionMs >= 0);
        Assert.DoesNotContain("127.0.0.1", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Connection refused", json, StringComparison.OrdinalIgnoreCase);
    }
}
