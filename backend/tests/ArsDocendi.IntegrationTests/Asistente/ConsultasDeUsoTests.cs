using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Persistencia;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// El panel de uso, agregado de <c>registro_operativo</c>
/// (asistente-panel-de-uso, grupo 9 de asistente-administracion-de-uso).
/// </summary>
public sealed class ConsultasDeUsoTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_consultas_de_uso")
{
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    private static readonly DateTimeOffset Ancla = new(2026, 9, 25, 10, 0, 0, TimeSpan.Zero);

    // ---------------------------------------------------------------- 9.2

    [Fact]
    public async Task El_nombre_para_mostrar_se_resuelve_via_IConsultasIdentity_no_usuarios_ver()
    {
        await SembrarAsync();
        await SembrarFilaAsync(Secretaria, "anthropic/claude-sonnet-5", "Respondida", 2, 100, 50, 10);

        var panel = await Consultas().ObtenerAsync(
            new RangoDePeriodo(Ancla.AddDays(-1), Ancla.AddDays(1)),
            TestContext.Current.CancellationToken);

        var deSecretaria = Assert.Single(panel.PorUsuario, u => u.Clave == Secretaria.ToString());
        Assert.Equal("Lucía Fernández", deSecretaria.NombreParaMostrar);
    }

    // ---------------------------------------------------------------- 9.4

    [Fact]
    public async Task Una_fila_sin_precio_cuenta_aparte_y_no_como_costo_cero()
    {
        await SembrarAsync();
        await SembrarFilaAsync(Secretaria, "local/sin-precio", "Respondida", 1, 100, 50, 10);

        var panel = await Consultas().ObtenerAsync(
            new RangoDePeriodo(Ancla.AddDays(-1), Ancla.AddDays(1)),
            TestContext.Current.CancellationToken);

        Assert.Equal(0m, panel.Organizacion.CostoEstimado);
        Assert.Equal(1, panel.Organizacion.TurnosSinPrecio);
    }

    [Fact]
    public async Task Con_precio_el_costo_se_estima_y_las_filas_sin_precio_quedan_en_cero()
    {
        await SembrarAsync();
        await SembrarPrecioAsync("anthropic", "claude-sonnet-5", 0.001m);
        await SembrarFilaAsync(Secretaria, "anthropic/claude-sonnet-5", "Respondida", 1, 1000, 0, 10);

        var panel = await Consultas().ObtenerAsync(
            new RangoDePeriodo(Ancla.AddDays(-1), Ancla.AddDays(1)),
            TestContext.Current.CancellationToken);

        Assert.Equal(1m, panel.Organizacion.CostoEstimado);
        Assert.Equal(0, panel.Organizacion.TurnosSinPrecio);
    }

    // ---------------------------------------------------------------- 9.1

    [Fact]
    public async Task Filas_fuera_del_periodo_no_se_agregan()
    {
        await SembrarAsync();
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO asistente.registro_operativo
                (actor_id, ocurrido_en, carril, estado, llamadas_al_modelo, tokens_de_entrada,
                 tokens_de_salida, latencia_ms, hubo_reintento, truncado)
            VALUES (@actor, @fuera, 'Sql', 'Respondida', 1, 10, 10, 5, false, false)
            """, conexion);
        comando.Parameters.AddWithValue("actor", Secretaria);
        comando.Parameters.AddWithValue("fuera", Ancla.AddDays(-100));
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        var panel = await Consultas().ObtenerAsync(
            new RangoDePeriodo(Ancla.AddDays(-1), Ancla.AddDays(1)),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, panel.Organizacion.Turnos);
    }

    // ------------------------------------------------------------------ apoyo

    private ConsultasDeUso Consultas() =>
        new(new CadenaDuena(Cadena), new ConsultasIdentity(PostgresFixture.CrearIdentity(Cadena)));

    private async Task SembrarFilaAsync(
        Guid actor, string proveedor, string estado, int llamadas, int entrada, int salida, int latenciaMs)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO asistente.registro_operativo
                (actor_id, ocurrido_en, carril, estado, llamadas_al_modelo, tokens_de_entrada,
                 tokens_de_salida, latencia_ms, hubo_reintento, truncado, proveedor)
            VALUES (@actor, @cuando, 'Sql', @estado, @llamadas, @entrada, @salida, @latencia, false, false, @proveedor)
            """, conexion);
        comando.Parameters.AddWithValue("actor", actor);
        comando.Parameters.AddWithValue("cuando", Ancla);
        comando.Parameters.AddWithValue("estado", estado);
        comando.Parameters.AddWithValue("llamadas", llamadas);
        comando.Parameters.AddWithValue("entrada", entrada);
        comando.Parameters.AddWithValue("salida", salida);
        comando.Parameters.AddWithValue("latencia", latenciaMs);
        comando.Parameters.AddWithValue("proveedor", proveedor);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task SembrarPrecioAsync(string proveedor, string modelo, decimal precioPorTokenEntrada)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO asistente.tabla_de_precios
                (proveedor, modelo, precio_por_token_entrada, precio_por_token_salida,
                 precio_por_token_cache, version, vigente_desde)
            VALUES (@proveedor, @modelo, @precio, 0, 0, 1, @vigente_desde)
            """, conexion);
        comando.Parameters.AddWithValue("proveedor", proveedor);
        comando.Parameters.AddWithValue("modelo", modelo);
        comando.Parameters.AddWithValue("precio", precioPorTokenEntrada);
        comando.Parameters.AddWithValue("vigente_desde", Ancla.AddDays(-2));
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
