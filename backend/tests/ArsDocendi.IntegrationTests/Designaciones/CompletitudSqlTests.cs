using ArsDocendi.IntegrationTests.Infraestructura;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Designaciones;

public sealed partial class VigenciasPedidosTests
{
    [Theory]
    [InlineData("cargo_solicitado_id=NULL")]
    [InlineData("horas=NULL")]
    [InlineData("horas=0")]
    public async Task SQL_no_permite_incompleto_en_circuito(string asignacion)
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand($"UPDATE designaciones.pedidos SET estado='en_revision_coordinador', {asignacion} WHERE id=@id", conexion);
        comando.Parameters.AddWithValue("id", PedidoId);
        var error = await Assert.ThrowsAsync<PostgresException>(() => comando.ExecuteNonQueryAsync(ct));
        Assert.Equal("23514", error.SqlState);
        Assert.Equal("pedidos_completitud_circuito", error.ConstraintName);
    }

    [Fact]
    public async Task SQL_no_permite_dedicacion_nula_en_circuito()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await using var conexion = await AbrirConexionAsync();
        await using (var desactivar = new NpgsqlCommand(
            "ALTER TABLE designaciones.pedidos DISABLE TRIGGER validar_dedicacion_pedido", conexion))
            await desactivar.ExecuteNonQueryAsync(ct);
        try
        {
            await using var comando = new NpgsqlCommand("""
                UPDATE designaciones.pedidos
                SET estado='en_revision_coordinador', dedicacion_solicitada_id=NULL
                WHERE id=@id
                """, conexion);
            comando.Parameters.AddWithValue("id", PedidoId);
            var error = await Assert.ThrowsAsync<PostgresException>(() => comando.ExecuteNonQueryAsync(ct));
            Assert.Equal("23514", error.SqlState);
            Assert.Equal("pedidos_completitud_circuito", error.ConstraintName);
        }
        finally
        {
            await using var activar = new NpgsqlCommand(
                "ALTER TABLE designaciones.pedidos ENABLE TRIGGER validar_dedicacion_pedido", conexion);
            await activar.ExecuteNonQueryAsync(ct);
        }
    }
}
