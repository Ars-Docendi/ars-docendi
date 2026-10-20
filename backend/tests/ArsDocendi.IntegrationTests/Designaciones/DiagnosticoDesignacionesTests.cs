using ArsDocendi.Shared.Persistencia;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Modules.Designaciones.Infrastructure;

namespace ArsDocendi.IntegrationTests.Designaciones;

public sealed partial class VigenciasPedidosTests
{
    [Fact]
    public async Task Diagnostico_local_emite_referencias_sin_datos_personales_y_no_escribe()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        var tipo = typeof(DesignacionesDbContext).Assembly.GetType("Modules.Designaciones.Infrastructure.DiagnosticoDesignaciones");
        Assert.NotNull(tipo);
        var diagnostico = Assert.IsAssignableFrom<IDiagnosticoIntegridadModulo>(Activator.CreateInstance(tipo, db));
        var registros = await diagnostico.LeerAsync(ct);
        Assert.Equal("Designaciones", diagnostico.Contexto);
        Assert.Contains(registros, r => r.Tipo == "designaciones-persona" && r.TipoDestino == "persona");
        Assert.Contains(registros, r => r.Tipo == "designaciones-materia" && r.TipoDestino == "materia");
        Assert.Equal(8 + 10, registros.Count(r => r.Tipo == "designaciones-persona"));
        Assert.DoesNotContain(registros, r => r.Clase == "hallazgo");
        Assert.Empty(db.ChangeTracker.Entries());
    }
}
