using ArsDocendi.IntegrationTests.Infraestructura;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Identity;

public sealed class IntegridadIdentityTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "identity_integridad")
{
    [Fact]
    public async Task Sql_rechaza_upn_equivalente_incluso_inactivo()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var conexion = await AbrirConexionAsync();
        await using (var primera = new NpgsqlCommand(
            "INSERT INTO identity.users(upn,display_name) VALUES (' Cuenta@Example.test ', 'Sintético');", conexion))
            await primera.ExecuteNonQueryAsync(ct);

        await using var equivalente = new NpgsqlCommand(
            "INSERT INTO identity.users(upn,display_name,is_active) VALUES ('cuenta@example.test','Otro',false);", conexion);
        var error = await Assert.ThrowsAsync<PostgresException>(() => equivalente.ExecuteNonQueryAsync(ct));

        Assert.Equal("23505", error.SqlState);
        Assert.Equal("users_upn_normalizado_key", error.ConstraintName);
    }
}
