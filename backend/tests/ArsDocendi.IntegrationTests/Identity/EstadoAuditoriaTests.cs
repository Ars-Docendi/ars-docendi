using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Auditing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Identity;

public sealed class EstadoAuditoriaTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "estado_auditoria")
{
    [Fact]
    public async Task Modelo_EF_no_tiene_cambios_pendientes()
    {
        await using var db = PostgresFixture.CrearIdentity(Cadena);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Escaneo_completo_detecta_fila_no_tocada_y_cotejo_incremental_declara_cobertura_parcial()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ds = NpgsqlDataSource.Create(Cadena);
        var verificador = new VerificadorEstadoAuditoria(ds);
        await using var conexion = await AbrirConexionAsync();
        await using (var insertar = new NpgsqlCommand("""
            INSERT INTO identity.roles (id, code, name, scope, es_sistema, is_active)
            VALUES (gen_random_uuid(), 'estado-test', 'Original', 'global', FALSE, TRUE)
            """, conexion))
            await insertar.ExecuteNonQueryAsync(ct);
        var nonce = Guid.NewGuid();
        var inicial = await verificador.VerificarAsync("test", nonce, null, ct);
        // Los roles sembrados antes de attach no tienen evidencia: no inventar baseline.
        Assert.False(inicial.CoincideConAuditoria);
        Assert.True(inicial.FilasSinEvidencia > 0);
        Assert.False(inicial.CoberturaIntegralBase);
        Assert.Contains("identity.roles", inicial.TablasCubiertas);
        await using (var alterar = new NpgsqlCommand("""
            ALTER TABLE identity.roles DISABLE TRIGGER USER;
            UPDATE identity.roles SET name = 'Cambio fuera de auditoria' WHERE code = 'estado-test';
            ALTER TABLE identity.roles ENABLE TRIGGER USER;
            """, conexion))
            await alterar.ExecuteNonQueryAsync(ct);
        var incremental = await verificador.VerificarAsync("test", nonce, inicial.Cursor, ct);
        Assert.True(incremental.CoincideConAuditoria);
        Assert.Equal("incremental", incremental.Modo);
        var completo = await verificador.VerificarAsync("test", nonce, null, ct);
        Assert.False(completo.CoincideConAuditoria);
        Assert.True(completo.Divergencias > inicial.Divergencias);
        Assert.NotEqual(inicial.Digest, completo.Digest);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Baja_fisica_y_soft_delete_auditadas_coinciden(bool fisica)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var conexion = await AbrirConexionAsync();
        await using (var insertar = new NpgsqlCommand("""
            INSERT INTO identity.roles (id, code, name, scope, es_sistema, is_active)
            VALUES (gen_random_uuid(), 'baja-test', 'Baja', 'global', FALSE, TRUE)
            """, conexion))
            await insertar.ExecuteNonQueryAsync(ct);
        await using (var baja = new NpgsqlCommand(fisica
            ? "DELETE FROM identity.roles WHERE code = 'baja-test'"
            : "UPDATE identity.roles SET is_active = FALSE WHERE code = 'baja-test'", conexion))
            await baja.ExecuteNonQueryAsync(ct);
        await using var ds = NpgsqlDataSource.Create(Cadena);
        Assert.True((await new VerificadorEstadoAuditoria(ds)
            .VerificarAsync("test", Guid.NewGuid(), 0, ct)).CoincideConAuditoria);
    }
}
