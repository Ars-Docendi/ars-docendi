using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Designaciones;

public sealed class IntegridadMateriaTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "integridad_materia")
{
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
