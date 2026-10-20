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
    public async Task Upgrade_storage_desde_baseline_preserva_clave_bytes_y_auditoria_y_rechaza_publicacion_legacy_ambigua()
    {
        var ct = TestContext.Current.CancellationToken;
        var builder = new NpgsqlConnectionStringBuilder(cadena) { Database = "postgres" };
        var nombre = $"storage_upgrade_{Guid.NewGuid():N}";
        await using (var administrador = new NpgsqlConnection(builder.ConnectionString))
        {
            await administrador.OpenAsync(ct);
            await using var comando = new NpgsqlCommand($"CREATE DATABASE \"{nombre}\"", administrador);
            await comando.ExecuteNonQueryAsync(ct);
        }
        builder.Database = nombre;
        var destino = builder.ConnectionString;
        try
        {
            await using (var identity = PostgresFixture.CrearIdentity(destino)) await identity.Database.MigrateAsync(ct);
            await using var db = PostgresFixture.CrearAlmacenamiento(destino);
            var migrador = db.GetService<IMigrator>();
            await migrador.MigrateAsync("20261005000100_BaselineStorage", ct);
            var id = Guid.NewGuid();
            var pendiente = Guid.NewGuid();
            var clave = $"archivos/{id:N}";
            var bytes = Encoding.UTF8.GetBytes("%PDF-1.7\nlegacy inmutable\n");
            var hash = ReglasArchivos.CalcularSha256(bytes);
            var ahora = DateTimeOffset.UtcNow;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO storage.archivos(id,proposito,ambiente,bucket,clave_objeto,nombre_original,mime_declarado,mime_detectado,tamano_bytes,sha256,estado,propietario_id,expira_en,confirmado_at)
                VALUES ({id},'cv','test',{Bucket},{clave},'legacy.pdf','application/pdf','application/pdf',{bytes.Length},{hash},'disponible',{Propietario},{ahora.AddHours(1)},{ahora});
                INSERT INTO storage.archivos(id,proposito,ambiente,bucket,clave_objeto,nombre_original,mime_declarado,tamano_bytes,estado,propietario_id,expira_en)
                VALUES ({pendiente},'cv','test',{Bucket},{"archivos/" + pendiente.ToString("N")},'pendiente.pdf','application/pdf',{bytes.Length},'pendiente',{Propietario},{ahora.AddHours(1)});
                """, ct);
            var real = new ProveedorSeaweedFs(cliente!);
            using var contenido = new MemoryStream(bytes);
            await real.SubirAsync(Bucket, clave, contenido, "application/pdf", bytes.Length, ct);
            var eventosAntes = await db.Database.SqlQuery<long>($"SELECT count(*) AS \"Value\" FROM audit.change_log").SingleAsync(ct);
            var preview = migrador.GenerateScript("20261005000100_BaselineStorage");
            Assert.Contains("reserva_id", preview);
            Assert.Equal(eventosAntes, await db.Database.SqlQuery<long>($"SELECT count(*) AS \"Value\" FROM audit.change_log").SingleAsync(ct));
            await migrador.MigrateAsync(cancellationToken: ct);
            var actual = await db.Archivos.AsNoTracking().SingleAsync(x => x.Id == id, ct);
            Assert.Equal(clave, actual.ClaveObjeto);
            Assert.Null(actual.IntentoId);
            Assert.Equal(hash, actual.Sha256);
            var servicio = CrearServicio(db);
            var descarga = await servicio.AbrirDescargaAsync(id, ct);
            Assert.NotNull(descarga);
            using var recibido = new MemoryStream();
            await descarga.Contenido.CopyToAsync(recibido, ct);
            await descarga.Contenido.DisposeAsync();
            Assert.Equal(bytes, recibido.ToArray());
            await Assert.ThrowsAsync<ExcepcionAplicacion>(() => servicio.ConfirmarCargaAsync(new(pendiente), Propietario, ct));
            Assert.Equal(eventosAntes, await db.Database.SqlQuery<long>($"SELECT count(*) AS \"Value\" FROM audit.change_log").SingleAsync(ct));
            await migrador.MigrateAsync(cancellationToken: ct);
            Assert.Equal(eventosAntes, await db.Database.SqlQuery<long>($"SELECT count(*) AS \"Value\" FROM audit.change_log").SingleAsync(ct));
            Assert.Equal(2, await db.Archivos.CountAsync(ct));
        }
        finally { await postgres.EliminarBaseAsync(destino); }
    }

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
