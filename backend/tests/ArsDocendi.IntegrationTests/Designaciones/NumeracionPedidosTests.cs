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

    [Fact]
    public async Task Upgrade_preserva_secuencia_y_funcion_es_no_op_al_reaplicar()
    {
        var cadena = await Postgres.CrearBaseMigradaAsync("numeracion_upgrade", "20261005000200_BaselineDesignaciones");
        try
        {
            await using var conexion = new NpgsqlConnection(cadena);
            await conexion.OpenAsync(TestContext.Current.CancellationToken);
            var pedidoHistorico = Guid.NewGuid();
            var periodoHistorico = Guid.NewGuid();
            var personaHistorica = Guid.NewGuid();
            var carreraHistorica = Guid.NewGuid();
            var materiaHistorica = Guid.NewGuid();
            await using (var semilla = new NpgsqlCommand("""
                INSERT INTO identity.carreras(id,code,name) VALUES (@carrera,@codigo,'Carrera sintética');
                INSERT INTO identity.materias(id,code,name) VALUES (@materia,@codigoMateria,'Materia sintética');
                INSERT INTO identity.personas(id,documento,nombre,apellido) VALUES (@persona,@documento,'Persona','Sintética');
                INSERT INTO designaciones.periodos(id,nombre,carga_desde,carga_hasta,impacto_desde,impacto_hasta)
                    VALUES (@periodo,'Período sintético',DATE '2026-01-01',DATE '2026-02-01',DATE '2026-03-01',DATE '2026-07-31');
                INSERT INTO designaciones.pedidos(id,numero,periodo_id,persona_id,materia_id,carrera_id,novedad,estado)
                    VALUES (@pedido,'2023-0017',@periodo,@persona,@materia,@carrera,'Sin novedad','borrador');
                """, conexion))
            {
                semilla.Parameters.AddWithValue("pedido", pedidoHistorico);
                semilla.Parameters.AddWithValue("periodo", periodoHistorico);
                semilla.Parameters.AddWithValue("persona", personaHistorica);
                semilla.Parameters.AddWithValue("carrera", carreraHistorica);
                semilla.Parameters.AddWithValue("materia", materiaHistorica);
                semilla.Parameters.AddWithValue("codigo", $"C-{carreraHistorica:N}");
                semilla.Parameters.AddWithValue("codigoMateria", "99999");
                semilla.Parameters.AddWithValue("documento", $"D-{personaHistorica:N}");
                await semilla.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }
            await using var contarAuditoria = new NpgsqlCommand("SELECT count(*) FROM audit.change_log", conexion);
            var auditoriaAntes = await contarAuditoria.ExecuteScalarAsync(TestContext.Current.CancellationToken);
            await using (var fijar = new NpgsqlCommand(
                "SELECT setval('designaciones.pedidos_numero_seq', 10001, false)", conexion))
                await fijar.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            await using var db = PostgresFixture.CrearDesignaciones(cadena);
            await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
            await using (var verificarHistorico = new NpgsqlCommand(
                "SELECT numero FROM designaciones.pedidos WHERE id=@id", conexion))
            {
                verificarHistorico.Parameters.AddWithValue("id", pedidoHistorico);
                Assert.Equal("2023-0017", await verificarHistorico.ExecuteScalarAsync(TestContext.Current.CancellationToken));
            }
            Assert.Equal(auditoriaAntes, await contarAuditoria.ExecuteScalarAsync(TestContext.Current.CancellationToken));
            Assert.EndsWith("-10001", await SiguienteAsync(conexion, TestContext.Current.CancellationToken));
            await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
            Assert.EndsWith("-10002", await SiguienteAsync(conexion, TestContext.Current.CancellationToken));
        }
        finally { await Postgres.EliminarBaseAsync(cadena); }
    }

    private static async Task<string> SiguienteAsync(NpgsqlConnection conexion, CancellationToken cancellationToken)
    {
        await using var comando = new NpgsqlCommand("SELECT designaciones.siguiente_numero_pedido()", conexion);
        return (string)(await comando.ExecuteScalarAsync(cancellationToken))!;
    }
}
