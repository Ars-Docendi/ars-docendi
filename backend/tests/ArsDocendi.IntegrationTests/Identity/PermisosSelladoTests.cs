using ArsDocendi.IntegrationTests.Infraestructura;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Identity;

public sealed class PermisosSelladoTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "permisos_sellado")
{
    [Fact]
    public async Task Api_puede_auditar_sin_poder_alterar_log_cursor_ni_triggers()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var conexion = await AbrirConexionAsync();
        var rol = "api_test_" + Guid.NewGuid().ToString("N");
        await using (var crearRol = new NpgsqlCommand($"CREATE ROLE {rol} NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE", conexion))
            await crearRol.ExecuteNonQueryAsync(ct);
        try
        {
            await using (var crear = new NpgsqlCommand($"""
                GRANT USAGE ON SCHEMA identity, audit TO {rol};
                GRANT SELECT, INSERT, UPDATE, DELETE ON identity.roles TO {rol};
                SET ROLE {rol};
                INSERT INTO identity.roles(id,code,name,scope,es_sistema,is_active)
                VALUES(gen_random_uuid(),'permisos-test','Permisos','global',false,true);
                """, conexion))
                await crear.ExecuteNonQueryAsync(ct);
            foreach (var sql in new[] {
                "UPDATE audit.change_log SET new_row = '{}'::jsonb",
                "DELETE FROM audit.change_log", "TRUNCATE identity.roles",
                "UPDATE audit.seal_cursor SET last_seq = 0",
                "DELETE FROM audit.seal_batches", "ALTER TABLE identity.roles DISABLE TRIGGER USER",
                "SELECT audit.next_seal_seq()" })
            {
                await using var denegado = new NpgsqlCommand(sql, conexion);
                var error = await Assert.ThrowsAsync<PostgresException>(() => denegado.ExecuteNonQueryAsync(ct));
                Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
            }
        }
        finally
        {
            await using var limpiar = new NpgsqlCommand($"RESET ROLE; DROP OWNED BY {rol}; DROP ROLE {rol};", conexion);
            await limpiar.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }
}
