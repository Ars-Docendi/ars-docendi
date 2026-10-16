using ArsDocendi.IntegrationTests.Infraestructura;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// La forma de <c>database/asistente/006_asistente_administracion.sql</c>
/// contra una base migrada de verdad (asistente-administracion-de-uso, tarea
/// 2.1/2.2).
/// </summary>
/// <remarks>
/// Igual que <see cref="MigracionDelAsistenteTests"/> prueba el DDL contra el
/// mismo código que corre el migrador —no una copia—, acá se apoya en que
/// <see cref="PostgresFixture.CrearBaseMigradaAsync"/> ya corre
/// <c>AdministracionAsistente.AplicarAsync</c> para cada base aislada.
/// </remarks>
public sealed class AdministracionDeUsoMigracionTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_administracion_migracion")
{
    [Theory]
    [InlineData("presupuesto_rol")]
    [InlineData("presupuesto_usuario")]
    [InlineData("tope_organizacional")]
    [InlineData("consumo_organizacional_mensual")]
    [InlineData("tabla_de_precios")]
    [InlineData("modo_mantenimiento")]
    [InlineData("auditoria_administracion")]
    public async Task La_tabla_existe_en_el_schema_asistente(string tabla)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT count(*)
              FROM information_schema.tables
             WHERE table_schema = 'asistente' AND table_name = @tabla
            """, conexion);
        comando.Parameters.AddWithValue("tabla", tabla);

        var existe = (long)(await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;

        Assert.Equal(1, existe);
    }

    [Fact]
    public async Task Presupuesto_rol_tiene_las_columnas_esperadas()
    {
        await AssertColumnasAsync(
            "presupuesto_rol", "rol_code", "cupo_diario_turnos", "actualizado_en");
    }

    [Fact]
    public async Task Presupuesto_usuario_tiene_las_columnas_esperadas()
    {
        await AssertColumnasAsync(
            "presupuesto_usuario",
            "actor_id", "cupo_diario_turnos", "vigente_desde", "vigente_hasta");
    }

    [Fact]
    public async Task Tope_organizacional_tiene_las_columnas_esperadas()
    {
        await AssertColumnasAsync("tope_organizacional", "tope_mensual_usd", "vigente_desde");
    }

    [Fact]
    public async Task Consumo_organizacional_mensual_tiene_las_columnas_esperadas()
    {
        await AssertColumnasAsync(
            "consumo_organizacional_mensual", "anio", "mes", "costo_estimado_acumulado");
    }

    [Fact]
    public async Task Tabla_de_precios_tiene_las_columnas_esperadas()
    {
        await AssertColumnasAsync(
            "tabla_de_precios",
            "proveedor", "modelo", "precio_por_token_entrada", "precio_por_token_salida",
            "precio_por_token_cache", "version", "vigente_desde", "vigente_hasta");
    }

    [Fact]
    public async Task Modo_mantenimiento_tiene_las_columnas_esperadas_y_una_sola_fila()
    {
        await AssertColumnasAsync(
            "modo_mantenimiento", "activo", "razon", "actor_id", "actualizado_en");

        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            "SELECT count(*) FROM asistente.modo_mantenimiento", conexion);

        var filas = (long)(await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
        Assert.Equal(1, filas);
    }

    [Fact]
    public async Task Un_segundo_intento_de_fila_en_modo_mantenimiento_es_rechazado()
    {
        await using var conexion = await AbrirConexionAsync();

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            new NpgsqlCommand(
                "INSERT INTO asistente.modo_mantenimiento (id, activo, actualizado_en) VALUES (2, false, now())",
                conexion).ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

        // check_violation: el CHECK id = 1 es lo que impide una segunda fila.
        Assert.Equal("23514", error.SqlState);
    }

    [Fact]
    public async Task Auditoria_administracion_tiene_las_columnas_esperadas()
    {
        await AssertColumnasAsync(
            "auditoria_administracion", "actor_id", "ocurrido_en", "accion", "antes", "despues");
    }

    [Fact]
    public async Task Reaplicar_la_migracion_no_duplica_ni_falla()
    {
        await using var conexion = await AbrirConexionAsync();

        await Modules.Asistente.Infrastructure.AdministracionAsistente.AplicarAsync(
            conexion, TestContext.Current.CancellationToken);
        await Modules.Asistente.Infrastructure.AdministracionAsistente.AplicarAsync(
            conexion, TestContext.Current.CancellationToken);

        await using var comando = new NpgsqlCommand(
            "SELECT count(*) FROM asistente.presupuesto_rol", conexion);
        var filas = (long)(await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;

        Assert.Equal(7, filas);
    }

    private async Task AssertColumnasAsync(string tabla, params string[] columnas)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT column_name
              FROM information_schema.columns
             WHERE table_schema = 'asistente' AND table_name = @tabla
            """, conexion);
        comando.Parameters.AddWithValue("tabla", tabla);

        await using var lector = await comando.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var presentes = new HashSet<string>(StringComparer.Ordinal);
        while (await lector.ReadAsync(TestContext.Current.CancellationToken))
        {
            presentes.Add(lector.GetString(0));
        }

        foreach (var columna in columnas)
        {
            Assert.Contains(columna, presentes);
        }
    }
}
