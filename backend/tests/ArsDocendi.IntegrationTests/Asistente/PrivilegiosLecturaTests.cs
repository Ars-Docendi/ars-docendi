using ArsDocendi.IntegrationTests.Infraestructura;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Comprueba los privilegios del asistente donde importa: conectado como los roles
/// reales, contra la base real.
/// </summary>
/// <remarks>
/// El test de manifiesto compara catálogos; éste ejecuta consultas. Son dos cosas
/// distintas: el manifiesto puede coincidir con los catálogos y aun así el límite
/// no comportarse como se espera —por ejemplo si `SELECT *` sobre una tabla con
/// columnas no concedidas devolviera algo en vez de fallar—. Acá se prueba el
/// comportamiento del motor, que es el que sostiene la garantía.
/// </remarks>
[Collection(ColeccionPostgres.Nombre)]
public sealed class PrivilegiosLecturaTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_privilegios")
{
    /// <summary>Código SQLSTATE de <c>insufficient_privilege</c>.</summary>
    private const string PrivilegioInsuficiente = "42501";

    [Fact]
    public async Task Una_columna_personal_la_lee_solo_el_rol_con_datos_personales()
    {
        await SembrarPersonaAsync();

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            ConsultarComoAsistenteAsync(false, "SELECT documento FROM identity.personas"));
        Assert.Equal(PrivilegioInsuficiente, error.SqlState);

        var conPii = await ContarComoAsistenteAsync(true, "SELECT documento FROM identity.personas");
        Assert.Equal(1, conPii);
    }

    [Fact]
    public async Task Un_select_estrella_sobre_personas_falla_con_el_rol_basico()
    {
        await SembrarPersonaAsync();

        // La tabla tiene columnas no concedidas, así que el asterisco las alcanza.
        // Es el comportamiento buscado: la restricción la impone el motor, y una
        // consulta perezosa falla en vez de devolver de más.
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            ConsultarComoAsistenteAsync(false, "SELECT * FROM identity.personas"));

        Assert.Equal(PrivilegioInsuficiente, error.SqlState);
    }

    [Fact]
    public async Task Las_columnas_concedidas_se_leen_con_el_rol_basico()
    {
        await SembrarPersonaAsync();

        var filas = await ContarComoAsistenteAsync(
            false, "SELECT id, legajo, nombre, apellido FROM identity.personas");

        Assert.Equal(1, filas);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task El_schema_de_auditoria_es_inalcanzable(bool conDatosPersonales)
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            ConsultarComoAsistenteAsync(conDatosPersonales, "SELECT * FROM audit.change_log"));

        Assert.Equal(PrivilegioInsuficiente, error.SqlState);
        Assert.Contains("audit", error.MessageText, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task La_cache_de_idempotencia_es_inalcanzable(bool conDatosPersonales)
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            ConsultarComoAsistenteAsync(
                conDatosPersonales, "SELECT * FROM designaciones.idempotencia_comandos"));

        Assert.Equal(PrivilegioInsuficiente, error.SqlState);
    }

    [Theory]
    [InlineData(false, "azure_oid", "identity.users")]
    [InlineData(true, "azure_oid", "identity.users")]
    [InlineData(false, "granted_by", "identity.user_roles")]
    [InlineData(true, "granted_by", "identity.user_roles")]
    [InlineData(false, "snapshot", "designaciones.pedidos")]
    [InlineData(true, "snapshot", "designaciones.pedidos")]
    [InlineData(false, "uri", "designaciones.pedido_adjuntos")]
    [InlineData(true, "uri", "designaciones.pedido_adjuntos")]
    public async Task Una_columna_denegada_no_se_lee_con_ninguno_de_los_dos_roles(
        bool conDatosPersonales, string columna, string tabla)
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            ConsultarComoAsistenteAsync(conDatosPersonales, $"SELECT {columna} FROM {tabla}"));

        Assert.Equal(PrivilegioInsuficiente, error.SqlState);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ningun_rol_puede_mutar(bool conDatosPersonales)
    {
        string[] mutaciones =
        [
            "INSERT INTO designaciones.periodos (id, nombre, carga_desde, carga_hasta, impacto_desde, impacto_hasta) "
                + "VALUES (gen_random_uuid(), 'x', DATE '2026-01-01', DATE '2026-02-01', DATE '2026-03-01', DATE '2026-12-31')",
            "UPDATE designaciones.periodos SET nombre = 'x'",
            "DELETE FROM designaciones.periodos",
            "TRUNCATE designaciones.periodos",
            "UPDATE identity.personas SET nombre = 'x'",
            "DELETE FROM identity.personas",
        ];

        foreach (var sentencia in mutaciones)
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() =>
                ConsultarComoAsistenteAsync(conDatosPersonales, sentencia));

            Assert.Equal(PrivilegioInsuficiente, error.SqlState);
        }
    }

    [Fact]
    public async Task La_extension_unaccent_quedo_instalada()
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            "SELECT count(*) FROM pg_extension WHERE extname = 'unaccent'", conexion);

        var instaladas = (long)(await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;

        Assert.Equal(1, instaladas);
    }

    [Fact]
    public async Task El_search_path_del_asistente_obliga_a_calificar_los_nombres()
    {
        // Contrapartida del search_path vacío: sin schema, el nombre no resuelve.
        // Sostiene que la SQL generada tenga que decir qué tabla toca.
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            ConsultarComoAsistenteAsync(false, "SELECT id FROM personas"));

        Assert.Equal("42P01", error.SqlState);
    }

    // ------------------------------------------------------------------------------ apoyo

    private async Task SembrarPersonaAsync()
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO identity.personas (id, documento, nombre, apellido, legajo)
            VALUES (gen_random_uuid(), @documento, 'Barbara', 'Liskov', @legajo)
            """, conexion);
        comando.Parameters.AddWithValue("documento", $"D-{Guid.NewGuid():N}");
        comando.Parameters.AddWithValue("legajo", $"L-{Guid.NewGuid():N}"[..12]);

        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task<int> ContarComoAsistenteAsync(bool conDatosPersonales, string sql)
    {
        await using var conexion = await AbrirConexionComoAsistenteAsync(conDatosPersonales);
        await using var comando = new NpgsqlCommand(sql, conexion);
        await using var lector = await comando.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        var filas = 0;
        while (await lector.ReadAsync(TestContext.Current.CancellationToken))
        {
            filas++;
        }

        return filas;
    }

    private async Task ConsultarComoAsistenteAsync(bool conDatosPersonales, string sql)
    {
        await using var conexion = await AbrirConexionComoAsistenteAsync(conDatosPersonales);
        await using var comando = new NpgsqlCommand(sql, conexion);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
