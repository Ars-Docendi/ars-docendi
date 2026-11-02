using System.Text;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Storage.Contracts;
using ArsDocendi.Storage.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Storage;

public sealed partial class AlmacenamientoSeaweedFsTests
{
    [Fact]
    public async Task Trigger_real_impide_cambiar_hash_o_clave_de_publicacion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = PostgresFixture.CrearAlmacenamiento(cadena);
        var servicio = CrearServicio(db);
        var id = await SubirPdfAsync(servicio, "inmutable.pdf");
        var antes = await db.Archivos.AsNoTracking().SingleAsync(x => x.Id == id, ct);
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE storage.archivos SET sha256='00' WHERE id={id}", ct));
        Assert.Equal("23514", error.SqlState);
        error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE storage.archivos SET clave_objeto='otra' WHERE id={id}", ct));
        Assert.Equal("23514", error.SqlState);
        var despues = await db.Archivos.AsNoTracking().SingleAsync(x => x.Id == id, ct);
        Assert.Equal(antes.ClaveObjeto, despues.ClaveObjeto);
        Assert.Equal(antes.Sha256, despues.Sha256);
        var batch = await servicio.ObtenerPorIdsAsync([id, id, Guid.NewGuid()], ct);
        Assert.Single(batch);
        Assert.Equal(antes.Sha256, batch[id].Sha256);
        var registros = await new DiagnosticoIntegridadAlmacenamiento(db).LeerAsync(ct);
        var registro = Assert.Single(registros);
        Assert.Equal("destino", registro.Clase);
        Assert.Equal("archivo", registro.Tipo);
        Assert.Equal(id, registro.Id);
        Assert.Null(registro.TipoDestino);
    }
}
