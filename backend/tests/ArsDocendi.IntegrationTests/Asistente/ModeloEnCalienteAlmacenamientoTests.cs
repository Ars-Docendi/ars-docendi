using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Persistencia;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Storage of the runtime model: the three tables, the repositories and the encrypted key lifecycle
/// against a real database (asistente-modelo-en-caliente, slice 1: tasks 1.1-1.3, 1.6, 1.7, 1.10, 1.11).
/// </summary>
public sealed class ModeloEnCalienteAlmacenamientoTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_modelo_en_caliente")
{
    private const string Secreto = "sk-live-ABCDEFGH12345678WXYZ";
    private static readonly Guid Admin = Guid.Parse("a0000000-0000-4000-8000-0000000000a1");
    private static readonly DateTimeOffset Ancla = new(2027, 6, 15, 14, 37, 12, TimeSpan.Zero);

    private CadenaDuena Duena => new(Cadena);

    private RepositorioDeCredenciales Credenciales(CifradoDeCredenciales cifrado) =>
        new(Duena, cifrado, new RelojFijo(Ancla));

    private static CifradoDeCredenciales Cifrado(byte actual) =>
        CifradoDeCredencialesTests.Cifrado(CifradoDeCredencialesTests.ClaveMaestra(actual));

    // ------------------------------------------------------------- the DDL (1.1, 1.7)

    [Fact]
    public async Task Applying_the_model_DDL_twice_changes_nothing_and_keeps_the_seed_stable()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var conexion = await AbrirConexionAsync();

        var antes = await ContarCatalogoAsync(conexion, ct);

        await ModeloAsistente.AplicarAsync(conexion, ct);
        await ModeloAsistente.AplicarAsync(conexion, ct);

        Assert.Equal(antes, await ContarCatalogoAsync(conexion, ct));
        Assert.Equal(0L, await EscalarAsync(conexion, "SELECT count(*) FROM asistente.modelo_vigente", ct));
    }

    [Fact]
    public async Task The_selection_table_refuses_a_second_row()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var conexion = await AbrirConexionAsync();

        var error = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var comando = new NpgsqlCommand(
                """
                INSERT INTO asistente.modelo_vigente (id, empresa, modelo, actor_id, actualizado_en)
                VALUES (2, 'openai', 'gpt-5', @actor, now())
                """, conexion);
            comando.Parameters.AddWithValue("actor", Admin);
            await comando.ExecuteNonQueryAsync(ct);
        });

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    }

    // ------------------------------------------------- selection and catalog (1.6)

    [Fact]
    public async Task With_no_stored_selection_the_repository_reports_none()
    {
        var repositorio = new RepositorioDelModelo(Duena, new RelojFijo(Ancla));

        Assert.Null(await repositorio.LeerSeleccionAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Applying_a_catalog_model_stores_it_and_a_later_one_replaces_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new RepositorioDelModelo(Duena, new RelojFijo(Ancla));

        Assert.True(await repositorio.AplicarSeleccionAsync("openai", "gpt-5", Admin, ct));
        Assert.True(await repositorio.AplicarSeleccionAsync("ollama", "qwen3.5:9b", Admin, ct));

        var seleccion = await repositorio.LeerSeleccionAsync(ct);
        Assert.Equal(new SeleccionDeModelo("ollama", "qwen3.5:9b", Admin, Ancla), seleccion);
    }

    [Theory]
    [InlineData("openai", "gpt-no-existe")]
    [InlineData("otra-empresa", "gpt-5")]
    [InlineData("OPENAI", "gpt-5")]
    public async Task A_model_outside_the_catalog_is_rejected_and_the_selection_is_unchanged(
        string empresa, string modelo)
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new RepositorioDelModelo(Duena, new RelojFijo(Ancla));
        Assert.True(await repositorio.AplicarSeleccionAsync("anthropic", "claude-sonnet-5", Admin, ct));

        Assert.False(await repositorio.AplicarSeleccionAsync(empresa, modelo, Admin, ct));

        var seleccion = await repositorio.LeerSeleccionAsync(ct);
        Assert.Equal("anthropic", seleccion?.Empresa);
        Assert.Equal("claude-sonnet-5", seleccion?.Modelo);
    }

    [Fact]
    public async Task A_rejected_first_selection_leaves_no_row()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new RepositorioDelModelo(Duena, new RelojFijo(Ancla));

        Assert.False(await repositorio.AplicarSeleccionAsync("openai", "gpt-no-existe", Admin, ct));

        Assert.Null(await repositorio.LeerSeleccionAsync(ct));
    }

    [Fact]
    public async Task Resetting_deletes_the_selection_and_reports_whether_there_was_one()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new RepositorioDelModelo(Duena, new RelojFijo(Ancla));

        Assert.False(await repositorio.RestablecerAsync(ct));
        await repositorio.AplicarSeleccionAsync("openai", "gpt-5", Admin, ct);
        Assert.True(await repositorio.RestablecerAsync(ct));
        Assert.Null(await repositorio.LeerSeleccionAsync(ct));
    }

    // ------------------------------------------------------- credentials (1.6, 1.10)

    [Fact]
    public async Task A_saved_key_is_stored_encrypted_and_only_the_hint_is_listed()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = Credenciales(Cifrado(1));

        var pista = await repositorio.GuardarAsync("openai", Secreto, Admin, ct);

        Assert.Equal("WXYZ", pista);

        var estados = await repositorio.ListarAsync(ct);
        var estado = Assert.Single(estados);
        Assert.Equal(new EstadoDeCredencial("openai", "WXYZ", null, Ancla), estado);
        Assert.DoesNotContain(Secreto, estado.ToString(), StringComparison.Ordinal);

        await using var conexion = await AbrirConexionAsync();
        var guardado = (string)(await EscalarAsync(
            conexion, "SELECT clave_cifrada FROM asistente.credencial_de_proveedor WHERE empresa = 'openai'", ct))!;
        Assert.StartsWith("v1:", guardado, StringComparison.Ordinal);
        Assert.DoesNotContain(Secreto, guardado, StringComparison.Ordinal);

        var abierta = await repositorio.AbrirAsync("openai", ct);
        Assert.Equal(Secreto, abierta?.Clave);
        Assert.DoesNotContain(Secreto, abierta!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Opening_a_company_with_no_key_returns_null()
    {
        Assert.Null(await Credenciales(Cifrado(1)).AbrirAsync("mistral", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Replacing_a_key_keeps_one_row_per_company_and_clears_the_validation()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = Credenciales(Cifrado(1));

        await repositorio.GuardarAsync("openai", Secreto, Admin, ct);
        await repositorio.RegistrarValidacionAsync("openai", ct);
        Assert.NotNull(Assert.Single(await repositorio.ListarAsync(ct)).ValidadaEn);

        var pista = await repositorio.GuardarAsync("openai", "sk-live-otra-clave-9999", Admin, ct);

        Assert.Equal("9999", pista);
        var estado = Assert.Single(await repositorio.ListarAsync(ct));
        Assert.Null(estado.ValidadaEn);
        Assert.Equal("sk-live-otra-clave-9999", (await repositorio.AbrirAsync("openai", ct))?.Clave);
    }

    [Fact]
    public async Task A_key_too_short_to_hide_behind_a_hint_is_rejected_without_echoing_it()
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            Credenciales(Cifrado(1)).GuardarAsync("openai", "abc1234", Admin, TestContext.Current.CancellationToken));

        Assert.DoesNotContain("abc1234", error.Message, StringComparison.Ordinal);
        Assert.Empty(await Credenciales(Cifrado(1)).ListarAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Rotation_re_encrypts_a_value_under_the_previous_key_on_read()
    {
        var ct = TestContext.Current.CancellationToken;
        await Credenciales(Cifrado(1)).GuardarAsync("google", Secreto, Admin, ct);

        await using var conexion = await AbrirConexionAsync();
        var antes = (string)(await EscalarAsync(
            conexion, "SELECT clave_cifrada FROM asistente.credencial_de_proveedor WHERE empresa = 'google'", ct))!;

        var rotado = Credenciales(CifradoDeCredencialesTests.Cifrado(
            CifradoDeCredencialesTests.ClaveMaestra(2), CifradoDeCredencialesTests.ClaveMaestra(1)));
        var abierta = await rotado.AbrirAsync("google", ct);

        Assert.Equal(Secreto, abierta?.Clave);

        var despues = (string)(await EscalarAsync(
            conexion, "SELECT clave_cifrada FROM asistente.credencial_de_proveedor WHERE empresa = 'google'", ct))!;
        Assert.NotEqual(antes, despues);
        Assert.NotEqual(antes.Split(':')[1], despues.Split(':')[1]);

        // With the previous key gone, the re-encrypted value opens under the current one alone.
        var soloActual = Credenciales(Cifrado(2));
        Assert.Equal(Secreto, (await soloActual.AbrirAsync("google", ct))?.Clave);
        // ...and the old master key can no longer open it.
        await Assert.ThrowsAsync<CredencialIlegibleException>(() =>
            Credenciales(Cifrado(1)).AbrirAsync("google", ct));
    }

    [Fact]
    public async Task A_row_tampered_in_the_database_or_moved_to_another_company_cannot_be_opened()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = Credenciales(Cifrado(1));
        await repositorio.GuardarAsync("openai", Secreto, Admin, ct);

        await using var conexion = await AbrirConexionAsync();
        await using (var mover = new NpgsqlCommand(
            """
            INSERT INTO asistente.credencial_de_proveedor
                (empresa, clave_cifrada, pista, validada_en, actor_id, actualizado_en)
            SELECT 'mistral', clave_cifrada, pista, NULL, actor_id, actualizado_en
              FROM asistente.credencial_de_proveedor WHERE empresa = 'openai'
            """, conexion))
        {
            await mover.ExecuteNonQueryAsync(ct);
        }

        await Assert.ThrowsAsync<CredencialIlegibleException>(() => repositorio.AbrirAsync("mistral", ct));
        Assert.Equal(Secreto, (await repositorio.AbrirAsync("openai", ct))?.Clave);
    }

    // --------------------------------------------------- no master key (1.11, repository)

    [Fact]
    public async Task Without_a_master_key_saving_reports_the_unavailable_key_and_stores_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var sinClave = Credenciales(CifradoDeCredencialesTests.Cifrado(string.Empty));

        await Assert.ThrowsAsync<ClaveMaestraNoDisponibleException>(() =>
            sinClave.GuardarAsync("openai", Secreto, Admin, ct));

        Assert.Empty(await sinClave.ListarAsync(ct));
    }

    [Fact]
    public async Task Without_a_master_key_the_local_selection_still_works()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new RepositorioDelModelo(Duena, new RelojFijo(Ancla));

        Assert.True(await repositorio.AplicarSeleccionAsync("ollama", "qwen3.5:9b", Admin, ct));
        Assert.Equal("ollama", (await repositorio.LeerSeleccionAsync(ct))?.Empresa);
    }

    // ---------------------------------------------------------------------- support

    private static async Task<long> ContarCatalogoAsync(NpgsqlConnection conexion, CancellationToken ct) =>
        await EscalarAsync(conexion, "SELECT count(*) FROM asistente.catalogo_de_modelos", ct) is long n ? n : -1;

    private static async Task<object?> EscalarAsync(NpgsqlConnection conexion, string sql, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(sql, conexion);
        return await comando.ExecuteScalarAsync(ct);
    }
}
