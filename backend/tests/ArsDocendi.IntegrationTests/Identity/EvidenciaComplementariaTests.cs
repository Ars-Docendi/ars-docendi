using System.Text.Json;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Auditing;
using Npgsql;
namespace ArsDocendi.IntegrationTests.Identity;

public sealed class EvidenciaComplementariaTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "evidencia_complementaria")
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Checkpoint_persistido_cubre_filas_iniciales_y_no_blanquea_cambios_directos(bool auditado)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ds = NpgsqlDataSource.Create(Cadena);
        await using var c = await AbrirConexionAsync();
        await using (var inicial = new NpgsqlCommand("""
            ALTER TABLE identity.roles DISABLE TRIGGER USER;
            INSERT INTO identity.roles(id,code,name,scope,es_sistema,is_active)
            VALUES(gen_random_uuid(),'baseline-test','Inicial','global',false,true);
            ALTER TABLE identity.roles ENABLE TRIGGER USER;
            """, c)) await inicial.ExecuteNonQueryAsync(ct);
        var preparador = new PreparadorLotesAuditoria(ds);
        var lote = (await preparador.PrepararSiguienteAsync("test", 100, ct)).Lote!;
        using var manifest = JsonDocument.Parse(lote.Manifiesto);
        Assert.True(manifest.RootElement.TryGetProperty("hashEstadoObservado", out _));
        await preparador.RegistrarFirmaAsync(lote.Id, "test", [1], ct);
        await preparador.RegistrarAcuseTestigoAsync(lote.Id, true, lote.HashManifiesto, ct);
        await preparador.RegistrarAcuseTestigoAsync(lote.Id, false, lote.HashManifiesto, ct);
        await using (var consulta = new NpgsqlCommand("SELECT status FROM audit.seal_baseline WHERE id=1", c))
            Assert.Equal("anchored", await consulta.ExecuteScalarAsync(ct));
        var sql = "UPDATE identity.roles SET description='cambio' WHERE code='baseline-test'";
        if (!auditado) sql = "ALTER TABLE identity.roles DISABLE TRIGGER USER;" + sql + ";ALTER TABLE identity.roles ENABLE TRIGGER USER;";
        await using (var cmd = new NpgsqlCommand(sql, c)) Assert.True(await cmd.ExecuteNonQueryAsync(ct) > 0);
        if (auditado)
        {
            Assert.True((await new VerificadorLotesAuditoria(ds).VerificarAsync("test", ct)).HashesLocalesValidos);
            Assert.NotNull((await preparador.PrepararSiguienteAsync("test", 100, ct)).Lote);
        }
        else
        {
            Assert.False((await new VerificadorLotesAuditoria(ds).VerificarAsync("test", ct)).HashesLocalesValidos);
            await Assert.ThrowsAsync<InvalidDataException>(() => preparador.PrepararSiguienteAsync("test", 100, ct));
        }
    }

    [Fact]
    public async Task Legacy_observado_se_incluye_en_manifiesto_y_su_alteracion_se_detecta()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ds = NpgsqlDataSource.Create(Cadena);
        await using var c = await AbrirConexionAsync();
        await using (var cmd = new NpgsqlCommand("""
            INSERT INTO audit.change_log(schema_name,table_name,row_pk,action,new_row,seal_seq)
            VALUES('identity','roles','legacy-sintetico','INSERT','{"name":"anterior"}',NULL);
            UPDATE audit.seal_baseline SET legacy_max_id=(SELECT max(id) FROM audit.change_log);
            """, c)) await cmd.ExecuteNonQueryAsync(ct);
        var lote = (await new PreparadorLotesAuditoria(ds).PrepararSiguienteAsync("test", 100, ct)).Lote!;
        using var manifest = JsonDocument.Parse(lote.Manifiesto);
        Assert.True(manifest.RootElement.TryGetProperty("hashLegacyObservado", out _));
        Assert.True((await new VerificadorLotesAuditoria(ds).VerificarAsync("test", ct)).HashesLocalesValidos);
        await using (var cmd = new NpgsqlCommand("UPDATE audit.change_log SET new_row='{}' WHERE seal_seq IS NULL", c))
            await cmd.ExecuteNonQueryAsync(ct);
        Assert.False((await new VerificadorLotesAuditoria(ds).VerificarAsync("test", ct)).HashesLocalesValidos);
    }
}
