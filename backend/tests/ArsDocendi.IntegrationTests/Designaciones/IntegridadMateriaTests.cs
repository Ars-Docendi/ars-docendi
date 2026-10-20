using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Designaciones;

public sealed class IntegridadMateriaTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "integridad_materia")
{
    [Theory]
    [InlineData("INSERT INTO designaciones.designaciones(persona_id,materia_id,carrera_id,cargo_id,dedicacion_id,horas,vigente_desde) SELECT persona_id,gen_random_uuid(),carrera_id,cargo_id,dedicacion_id,10,CURRENT_DATE FROM designaciones.designaciones LIMIT 1")]
    [InlineData("UPDATE designaciones.designaciones SET materia_id=gen_random_uuid()")]
    public async Task Materia_inexistente_se_rechaza_por_FK_simple(string sql)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var conexion = await AbrirConexionAsync();
        await PrepararAsync(conexion, ct);
        await using var comando = new NpgsqlCommand(sql, conexion);
        var error = await Assert.ThrowsAsync<PostgresException>(() => comando.ExecuteNonQueryAsync(ct));
        Assert.Equal("23503", error.SqlState);
        Assert.Equal("designaciones_materia_fk", error.ConstraintName);
    }

    [Fact]
    public async Task Materia_usada_solo_por_designacion_no_se_borra()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var conexion = await AbrirConexionAsync();
        await PrepararAsync(conexion, ct);
        await using var comando = new NpgsqlCommand("DELETE FROM identity.materias WHERE code='09999'", conexion);
        var error = await Assert.ThrowsAsync<PostgresException>(() => comando.ExecuteNonQueryAsync(ct));
        Assert.Contains(error.SqlState, new[] { "23503", "23001" });
        Assert.Equal("designaciones_materia_fk", error.ConstraintName);
    }

    [Fact]
    public async Task Upgrade_bloquea_huerfano_sin_reasignar_y_upgrade_limpio_preserva_filas()
    {
        var ct = TestContext.Current.CancellationToken;
        var baseDePrueba = await Postgres.CrearBaseMigradaAsync("materia_upgrade", "20261013155148_NumeracionSinTruncamientoDesignaciones");
        var cadena = baseDePrueba.Cadena;
        try
        {
            await using var conexion = new NpgsqlConnection(cadena);
            await conexion.OpenAsync(ct);
            await PrepararAsync(conexion, ct);
            await using (var alterar = new NpgsqlCommand("UPDATE designaciones.designaciones SET materia_id='ffffffff-ffff-4fff-8fff-ffffffffffff'", conexion))
                await alterar.ExecuteNonQueryAsync(ct);
            await using var db = PostgresFixture.CrearDesignaciones(cadena);
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.MigrateAsync(ct));
            await using (var verificar = new NpgsqlCommand("SELECT count(*) FROM designaciones.designaciones WHERE materia_id='ffffffff-ffff-4fff-8fff-ffffffffffff'", conexion))
                Assert.Equal(1L, await verificar.ExecuteScalarAsync(ct));
            await using (var repararFixture = new NpgsqlCommand("UPDATE designaciones.designaciones SET materia_id=(SELECT id FROM identity.materias WHERE code='09999')", conexion))
                await repararFixture.ExecuteNonQueryAsync(ct);
            await db.Database.MigrateAsync(ct);
            await using var catalogo = new NpgsqlCommand("SELECT convalidated FROM pg_constraint WHERE conname='designaciones_materia_fk'", conexion);
            Assert.Equal(true, await catalogo.ExecuteScalarAsync(ct));
            Assert.Single(await db.Designaciones.ToListAsync(ct));
        }
        finally { await Postgres.EliminarBaseAsync(baseDePrueba); }
    }

    private static async Task PrepararAsync(NpgsqlConnection conexion, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand("""
            INSERT INTO identity.materias(id,code,name) VALUES('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee','09999','Materia de ensayo');
            INSERT INTO identity.personas(id,documento,nombre,apellido) VALUES('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee','99999999','Ensayo','Materia');
            INSERT INTO identity.carreras(id,code,name) VALUES('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee','ENS','Ensayo');
            INSERT INTO designaciones.designaciones(persona_id,materia_id,carrera_id,cargo_id,dedicacion_id,horas,vigente_desde)
            VALUES('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee','eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee','eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee','c3000000-0000-4000-8000-000000000001','d6000000-0000-4000-8000-000000000001',10,CURRENT_DATE);
            """, conexion);
        await comando.ExecuteNonQueryAsync(ct);
    }
}
