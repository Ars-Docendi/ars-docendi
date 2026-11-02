using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Designaciones;

public sealed class NumeracionPedidosTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "numeracion_pedidos")
{
    [Theory]
    [InlineData(1000)]
    [InlineData(9999)]
    [InlineData(10000)]
    [InlineData(10001)]
    public async Task Conserva_el_correlativo_completo(long correlativo)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var fijar = new NpgsqlCommand(
            "SELECT setval('designaciones.pedidos_numero_seq', @valor, false)", conexion);
        fijar.Parameters.AddWithValue("valor", correlativo);
        await fijar.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        var numero = await SiguienteAsync(conexion, TestContext.Current.CancellationToken);
        Assert.EndsWith($"-{correlativo}", numero);
    }

    [Fact]
    public async Task Asignaciones_concurrentes_son_distintas_y_rollback_no_reutiliza_el_numero()
    {
        await using var conexion = await AbrirConexionAsync();
        await using (var fijar = new NpgsqlCommand(
            "SELECT setval('designaciones.pedidos_numero_seq', 9999, false)", conexion))
            await fijar.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        var numeros = await Task.WhenAll(Enumerable.Range(0, 16).Select(async _ =>
        {
            await using var otra = new NpgsqlConnection(Cadena);
            await otra.OpenAsync(TestContext.Current.CancellationToken);
            return await SiguienteAsync(otra, TestContext.Current.CancellationToken);
        }));
        Assert.Equal(16, numeros.Distinct().Count());
        string consumido;
        await using (var transaccion = await conexion.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            consumido = await SiguienteAsync(conexion, TestContext.Current.CancellationToken);
            await transaccion.RollbackAsync(TestContext.Current.CancellationToken);
        }
        var siguiente = await SiguienteAsync(conexion, TestContext.Current.CancellationToken);
        Assert.Equal(long.Parse(consumido.Split('-')[1]) + 1, long.Parse(siguiente.Split('-')[1]));
    }

    private static async Task<string> SiguienteAsync(NpgsqlConnection conexion, CancellationToken cancellationToken)
    {
        await using var comando = new NpgsqlCommand("SELECT designaciones.siguiente_numero_pedido()", conexion);
        return (string)(await comando.ExecuteScalarAsync(cancellationToken))!;
    }
}
