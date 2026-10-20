using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Storage.Contracts;
using Microsoft.EntityFrameworkCore;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure;

namespace ArsDocendi.IntegrationTests.Designaciones;

public sealed class ReferenciasArchivosDesignacionesTests(PostgresFixture postgres) : VigenciasPedidosBase(postgres)
{
    [Fact]
    public async Task Referencias_archivos_enlistan_misma_transaccion_y_rollback_no_deja_asociacion()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await using var db = PostgresFixture.CrearDesignaciones(Cadena);
        var tipo = typeof(DesignacionesDbContext).Assembly.GetType("Modules.Designaciones.Infrastructure.ReferenciasArchivosDesignaciones");
        Assert.NotNull(tipo);
        var adaptador = Activator.CreateInstance(tipo, db);
        var consulta = Assert.IsAssignableFrom<IConsultasReferenciasArchivos>(adaptador);
        var participante = Assert.IsAssignableFrom<IParticipanteTransaccionArchivos>(adaptador);
        Assert.Equal("Designaciones", consulta.Consumidor);
        var archivo = Guid.NewGuid();
        await using var conexion = await AbrirConexionAsync();
        await using var transaccion = await conexion.BeginTransactionAsync(ct);
        await participante.EnlistarAsync(conexion, transaccion, ct);
        db.PedidoAdjuntos.Add(new PedidoAdjunto { Id = Guid.NewGuid(), PedidoId = PedidoId, ArchivoId = archivo, Nombre = "ensayo.pdf", Tipo = TiposAdjunto.Cv });
        await db.SaveChangesAsync(ct);
        Assert.True(await consulta.TieneReferenciasAsync(archivo, ct));
        await transaccion.RollbackAsync(ct);
        await participante.DesenlistarAsync(ct);
        Assert.False(await consulta.TieneReferenciasAsync(archivo, ct));
    }
}
