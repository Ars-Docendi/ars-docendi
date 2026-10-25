using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArsDocendi.Host.Administracion;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Modules.Asistente;
using Modules.Asistente.Api;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// The price write path (asistente-modelo-en-caliente, slice 5: tasks 5.1, 5.2, 5.5-5.7)
/// over real HTTP and a real database.
/// </summary>
public sealed class ModeloEnCalientePreciosTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_modelo_precios")
{
    private const string Ruta = "/api/asistente/administracion";
    private static readonly Guid Sistemas = Guid.Parse("a0000000-0000-4000-8000-000000000007");
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private WebApplicationFactory<Program> CrearHost()
    {
        string A(string nombre) => $"{OpcionesAsistente.Seccion}:{nombre}";

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting($"ConnectionStrings:{CadenaDuena.Clave}", Cadena);
            builder.UseSetting($"{AutenticacionDesarrolloOptions.Seccion}:Enabled", "true");
            builder.UseSetting(A(nameof(OpcionesAsistente.RolSoloLectura)), RolSoloLectura);
            builder.UseSetting(A(nameof(OpcionesAsistente.RolSoloLecturaPii)), RolSoloLecturaPii);
            builder.UseSetting(A(nameof(OpcionesAsistente.PasswordSoloLectura)), PostgresFixture.PasswordDeRol);
            builder.UseSetting(A(nameof(OpcionesAsistente.PasswordSoloLecturaPii)), PostgresFixture.PasswordDeRol);
            builder.UseSetting(A(nameof(OpcionesAsistente.Proveedor)), "simulado");
            builder.UseSetting($"{OpcionesInferencia.Seccion}:Url", string.Empty);
            builder.UseSetting(A(nameof(OpcionesAsistente.UrlDelProveedorLocal)), string.Empty);
        });
    }

    private static HttpClient Cliente(WebApplicationFactory<Program> host, Guid usuario, string rol)
    {
        var cliente = host.CreateClient();
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, usuario.ToString());
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, rol);
        return cliente;
    }

    private static HttpClient Admin(WebApplicationFactory<Program> host) => Cliente(host, Sistemas, "sys_admin");

    private static Task<HttpResponseMessage> Cargar(
        HttpClient c, string empresa, string modelo, decimal? entrada, decimal? salida, decimal? cache, string? desde) =>
        c.PostAsJsonAsync(
            $"{Ruta}/precios",
            new { empresa, modelo, entradaPorMillon = entrada, salidaPorMillon = salida, cachePorMillon = cache, vigenteDesde = desde },
            Ct);

    private static async Task<JsonElement> CuerpoAsync(HttpResponseMessage respuesta) =>
        JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(Ct)).RootElement;

    private async Task<List<(int Version, decimal Entrada, decimal Salida, decimal Cache, DateTimeOffset Desde, DateTimeOffset? Hasta)>>
        FilasAsync(string proveedor, string modelo)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT version, precio_por_token_entrada, precio_por_token_salida, precio_por_token_cache,
                   vigente_desde, vigente_hasta
              FROM asistente.tabla_de_precios WHERE proveedor = @p AND modelo = @m ORDER BY version
            """, conexion);
        comando.Parameters.AddWithValue("p", proveedor);
        comando.Parameters.AddWithValue("m", modelo);

        var filas = new List<(int, decimal, decimal, decimal, DateTimeOffset, DateTimeOffset?)>();
        await using var lector = await comando.ExecuteReaderAsync(Ct);
        while (await lector.ReadAsync(Ct))
        {
            filas.Add((
                lector.GetInt32(0), lector.GetDecimal(1), lector.GetDecimal(2), lector.GetDecimal(3),
                lector.GetFieldValue<DateTimeOffset>(4),
                lector.IsDBNull(5) ? null : lector.GetFieldValue<DateTimeOffset>(5)));
        }

        return filas;
    }

    private async Task<List<(string Accion, string? Antes, string Despues)>> AuditoriaAsync()
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            "SELECT accion, antes, despues FROM asistente.auditoria_administracion ORDER BY ocurrido_en, id", conexion);
        await using var lector = await comando.ExecuteReaderAsync(Ct);

        var filas = new List<(string, string?, string)>();
        while (await lector.ReadAsync(Ct))
        {
            filas.Add((lector.GetString(0), lector.IsDBNull(1) ? null : lector.GetString(1), lector.GetString(2)));
        }

        return filas;
    }

    // -------------------------------------------------------------- 5.7 permission

    [Fact]
    public async Task The_price_route_answers_403_without_the_administration_permission_and_writes_nothing()
    {
        await SembrarAsync(Ct);
        using var host = CrearHost();
        using var cliente = Cliente(host, Secretaria, "secretaria");

        using var respuesta = await Cargar(cliente, "openai", "gpt-5", 1m, 2m, null, "2026-09-01");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Empty(await FilasAsync("openai", "gpt-5"));
        Assert.Empty(await AuditoriaAsync());
    }

    [Fact]
    public async Task The_price_route_does_not_collide_with_another_administration_route()
    {
        await SembrarAsync(Ct);
        using var host = CrearHost();
        using var cliente = Admin(host);
        _ = await cliente.GetAsync($"{Ruta}/modelo", Ct);

        var rutas = host.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>().Endpoints
            .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText!.StartsWith("api/asistente/administracion", StringComparison.Ordinal))
            .SelectMany(e => (e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods ?? ["*"])
                .Select(m => $"{m} {e.RoutePattern.RawText}"))
            .ToList();

        Assert.Equal(rutas.Count, rutas.Distinct().Count());
        Assert.Contains("POST api/asistente/administracion/precios", rutas);
    }

    // ---------------------------------------------------- 5.1 conversion and storage

    [Fact]
    public async Task A_price_is_stored_per_token_converted_exactly_with_decimal_arithmetic()
    {
        await SembrarAsync(Ct);
        using var host = CrearHost();
        using var cliente = Admin(host);

        // 0.07 and 1234.567891 are not representable as binary floats: a double round trip
        // would drift. The numeric(18,12) columns must hold the exact decimal quotient.
        using var respuesta = await Cargar(cliente, "openai", "gpt-5", 0.07m, 1234.567891m, 0.000001m, "2026-09-01");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await CuerpoAsync(respuesta);
        Assert.Equal(0.07m, cuerpo.GetProperty("entradaPorMillon").GetDecimal());
        Assert.Equal(1234.567891m, cuerpo.GetProperty("salidaPorMillon").GetDecimal());
        Assert.Equal(0.000001m, cuerpo.GetProperty("cachePorMillon").GetDecimal());
        Assert.Equal(1, cuerpo.GetProperty("version").GetInt32());
        Assert.Equal("2026-09-01", cuerpo.GetProperty("vigenteDesde").GetString());

        var fila = Assert.Single(await FilasAsync("openai", "gpt-5"));
        Assert.Equal(0.00000007m, fila.Entrada);
        Assert.Equal(0.001234567891m, fila.Salida);
        Assert.Equal(0.000000000001m, fila.Cache);
        Assert.Equal(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), fila.Desde);
        Assert.Null(fila.Hasta);
    }

    [Fact]
    public async Task A_missing_cache_price_is_stored_as_the_input_price_never_as_zero()
    {
        await SembrarAsync(Ct);
        using var host = CrearHost();
        using var cliente = Admin(host);

        using var respuesta = await Cargar(cliente, "openai", "gpt-5", 2m, 8m, null, "2026-09-01");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var fila = Assert.Single(await FilasAsync("openai", "gpt-5"));
        Assert.Equal(0.000002m, fila.Cache);
        Assert.Equal((await CuerpoAsync(respuesta)).GetProperty("cachePorMillon").GetDecimal(), 2m);
    }

    [Fact]
    public async Task A_free_local_model_can_be_priced_at_zero()
    {
        await SembrarAsync(Ct);
        using var host = CrearHost();
        using var cliente = Admin(host);

        using var respuesta = await Cargar(cliente, "ollama", "qwen3.5:9b", 0m, 0m, 0m, "2026-09-01");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Single(await FilasAsync("ollama", "qwen3.5:9b"));
    }

    // ---------------------------------------------------------- 5.5 versioning

    [Fact]
    public async Task Writing_a_price_closes_the_previous_version_and_opens_exactly_one_current_version()
    {
        await SembrarAsync(Ct);
        using var host = CrearHost();
        using var cliente = Admin(host);

        _ = await Cargar(cliente, "openai", "gpt-5", 3m, 15m, 0.3m, "2026-09-01");
        using var segunda = await Cargar(cliente, "openai", "gpt-5", 6m, 30m, 0.6m, "2026-09-20");
        using var tercera = await Cargar(cliente, "openai", "gpt-5", 7m, 31m, 0.7m, "2026-10-05");

        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
        Assert.Equal(HttpStatusCode.OK, tercera.StatusCode);

        var filas = await FilasAsync("openai", "gpt-5");
        Assert.Equal([1, 2, 3], filas.Select(f => f.Version));
        Assert.Single(filas, f => f.Hasta is null);
        Assert.Equal(filas[1].Desde, filas[0].Hasta);
        Assert.Equal(filas[2].Desde, filas[1].Hasta);
        Assert.Null(filas[2].Hasta);

        // The previous rows keep their amounts: nothing is edited in place.
        Assert.Equal(0.000003m, filas[0].Entrada);
        Assert.Equal(0.000006m, filas[1].Entrada);
    }

    [Fact]
    public async Task Two_concurrent_writes_for_the_same_model_leave_exactly_one_open_version()
    {
        await SembrarAsync(Ct);
        using var host = CrearHost();
        using var cliente = Admin(host);

        var respuestas = await Task.WhenAll(Enumerable.Range(1, 6).Select(i =>
            Cargar(cliente, "openai", "gpt-5", i, i, null, "2026-09-01")));

        Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var filas = await FilasAsync("openai", "gpt-5");
        Assert.Equal(6, filas.Count);
        Assert.Single(filas, f => f.Hasta is null);
        Assert.Equal(Enumerable.Range(1, 6), filas.Select(f => f.Version));
    }

    [Fact]
    public async Task Another_model_is_not_touched_by_a_price_write()
    {
        await SembrarAsync(Ct);
        using var host = CrearHost();
        using var cliente = Admin(host);

        _ = await Cargar(cliente, "openai", "gpt-5", 3m, 15m, null, "2026-09-01");
        _ = await Cargar(cliente, "ollama", "qwen3.5:9b", 0m, 0m, null, "2026-09-01");
        _ = await Cargar(cliente, "openai", "gpt-5", 4m, 16m, null, "2026-09-10");

        var otro = Assert.Single(await FilasAsync("ollama", "qwen3.5:9b"));
        Assert.Null(otro.Hasta);
        Assert.Equal(1, otro.Version);
    }

    // ----------------------------------------------- 5.6 readers and past moments

    [Fact]
    public async Task Usage_recorded_before_a_price_change_keeps_the_old_version_and_unpriced_rows_stay_unpriced()
    {
        await SembrarAsync(Ct);
        using var host = CrearHost();
        using var cliente = Admin(host);

        // Rows before the first price: not priced. Between the versions: v1. After: v2.
        await InsertarUsoAsync(new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero), "openai/gpt-5");
        await InsertarUsoAsync(new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero), "openai/gpt-5");
        await InsertarUsoAsync(new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero), "openai/gpt-5");

        _ = await Cargar(cliente, "openai", "gpt-5", 3m, 15m, null, "2026-09-01");
        _ = await Cargar(cliente, "openai", "gpt-5", 6m, 30m, null, "2026-09-20");

        var consultas = new ConsultasDeUso(new CadenaDuena(Cadena), new ConsultasIdentity(PostgresFixture.CrearIdentity(Cadena)));
        var panel = await consultas.ObtenerAsync(
            new RangoDePeriodo(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero)),
            Ct);

        // 1,000,000 input tokens per row: 3 USD at the v1 price, 6 USD at the v2 price.
        Assert.Equal(9m, panel.Organizacion.CostoEstimado);
        Assert.Equal(1, panel.Organizacion.TurnosSinPrecio);
    }

    [Fact]
    public async Task The_current_price_is_the_one_the_organizational_ceiling_reads_now()
    {
        await SembrarAsync(Ct);
        using var host = CrearHost();
        using var cliente = Admin(host);

        _ = await Cargar(cliente, "openai", "gpt-5", 3m, 15m, null, "2026-01-01");
        _ = await Cargar(cliente, "openai", "gpt-5", 6m, 30m, null, "2026-02-01");

        // The same half-open lookup both readers use, at three moments around the boundary.
        Assert.Equal(0.000003m, await EntradaVigenteEnAsync("openai", "gpt-5", new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.Zero)));
        Assert.Equal(0.000006m, await EntradaVigenteEnAsync("openai", "gpt-5", new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero)));
        Assert.Null(await EntradaVigenteEnAsync("openai", "gpt-5", new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero)));
    }

    // ------------------------------------------------------------- 5.2 validation

    public static TheoryData<string, string, decimal?, decimal?, decimal?, string?, string> Pedidos() => new()
    {
        { "openai", "gpt-5", -1m, 2m, null, "2026-09-01", "precio-negativo" },
        { "openai", "gpt-5", 1m, -0.000001m, null, "2026-09-01", "precio-negativo" },
        { "openai", "gpt-5", 1m, 2m, -3m, "2026-09-01", "precio-negativo" },
        { "openai", "gpt-5", 10000.01m, 2m, null, "2026-09-01", "precio-fuera-de-rango" },
        { "openai", "gpt-5", 1m, 2m, 99999m, "2026-09-01", "precio-fuera-de-rango" },
        { "openai", "gpt-5", 1.0000001m, 2m, null, "2026-09-01", "precio-con-demasiados-decimales" },
        { "openai", "gpt-5", null, 2m, null, "2026-09-01", "precio-obligatorio" },
        { "openai", "gpt-5", 1m, null, null, "2026-09-01", "precio-obligatorio" },
        { "openai", "gpt-5", 1m, 2m, null, null, "precio-obligatorio" },
        { "openai", "no-existe", 1m, 2m, null, "2026-09-01", "modelo-fuera-del-catalogo" },
        { "nadie", "gpt-5", 1m, 2m, null, "2026-09-01", "modelo-fuera-del-catalogo" },
    };

    [Theory]
    [MemberData(nameof(Pedidos))]
    public async Task An_invalid_price_is_a_400_with_a_code_and_writes_nothing(
        string empresa, string modelo, decimal? entrada, decimal? salida, decimal? cache, string? desde, string codigo)
    {
        await SembrarAsync(Ct);
        using var host = CrearHost();
        using var cliente = Admin(host);

        using var respuesta = await Cargar(cliente, empresa, modelo, entrada, salida, cache, desde);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(codigo, (await CuerpoAsync(respuesta)).GetProperty("codigo").GetString());
        Assert.Equal(0L, await EscalarAsync<long>("SELECT count(*) FROM asistente.tabla_de_precios"));
        Assert.Empty(await AuditoriaAsync());
    }

    [Fact]
    public async Task A_malformed_date_is_a_400_and_writes_nothing()
    {
        await SembrarAsync(Ct);
        using var host = CrearHost();
        using var cliente = Admin(host);

        using var respuesta = await Cargar(cliente, "openai", "gpt-5", 1m, 2m, null, "ayer");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(0L, await EscalarAsync<long>("SELECT count(*) FROM asistente.tabla_de_precios"));
    }

    [Fact]
    public async Task An_effective_date_before_the_current_version_start_is_rejected_and_changes_nothing()
    {
        await SembrarAsync(Ct);
        using var host = CrearHost();
        using var cliente = Admin(host);
        _ = await Cargar(cliente, "openai", "gpt-5", 3m, 15m, null, "2026-09-10");

        using var respuesta = await Cargar(cliente, "openai", "gpt-5", 9m, 9m, null, "2026-09-09");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(
            "vigencia-anterior-a-la-version-actual",
            (await CuerpoAsync(respuesta)).GetProperty("codigo").GetString());

        var fila = Assert.Single(await FilasAsync("openai", "gpt-5"));
        Assert.Null(fila.Hasta);
        Assert.Equal(0.000003m, fila.Entrada);
        Assert.Single(await AuditoriaAsync());
    }

    [Fact]
    public async Task An_effective_date_equal_to_the_current_version_start_is_accepted_and_the_new_row_wins()
    {
        await SembrarAsync(Ct);
        using var host = CrearHost();
        using var cliente = Admin(host);
        _ = await Cargar(cliente, "openai", "gpt-5", 3m, 15m, null, "2026-09-10");

        using var respuesta = await Cargar(cliente, "openai", "gpt-5", 4m, 16m, null, "2026-09-10");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var filas = await FilasAsync("openai", "gpt-5");
        Assert.Equal(filas[0].Desde, filas[0].Hasta); // an empty half-open range: no moment matches it
        Assert.Equal(
            0.000004m,
            await EntradaVigenteEnAsync("openai", "gpt-5", new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero)));
    }

    // -------------------------------------------------------------------- audit

    [Fact]
    public async Task Every_price_write_is_audited_with_the_previous_version_as_the_before_value()
    {
        await SembrarAsync(Ct);
        using var host = CrearHost();
        using var cliente = Admin(host);

        _ = await Cargar(cliente, "openai", "gpt-5", 3m, 15m, 0.3m, "2026-09-01");
        _ = await Cargar(cliente, "openai", "gpt-5", 6m, 30m, 0.6m, "2026-09-20");

        var filas = await AuditoriaAsync();
        Assert.Equal(2, filas.Count);
        Assert.All(filas, f => Assert.Equal("precio.cargar", f.Accion));

        Assert.Null(filas[0].Antes);
        var primera = JsonDocument.Parse(filas[0].Despues).RootElement;
        Assert.Equal("openai", primera.GetProperty("empresa").GetString());
        Assert.Equal("gpt-5", primera.GetProperty("modelo").GetString());
        Assert.Equal("3", primera.GetProperty("entradaPorMillon").GetString());
        Assert.Equal("0.3", primera.GetProperty("cachePorMillon").GetString());
        Assert.Equal("2026-09-01", primera.GetProperty("vigenteDesde").GetString());

        var antes = JsonDocument.Parse(filas[1].Antes!).RootElement;
        Assert.Equal("3", antes.GetProperty("entradaPorMillon").GetString());
        Assert.Equal("6", JsonDocument.Parse(filas[1].Despues).RootElement.GetProperty("entradaPorMillon").GetString());
    }

    [Fact]
    public async Task A_price_write_reaches_the_unified_feed_with_its_own_summary()
    {
        await SembrarAsync(Ct);
        using var host = CrearHost();
        using var cliente = Admin(host);
        _ = await Cargar(cliente, "openai", "gpt-5", 3m, 15m, 0.3m, "2026-09-01");
        _ = await Cargar(cliente, "openai", "gpt-5", 6m, 30m, 0.6m, "2026-09-20");

        var consultas = new ConsultasDeAuditoriaDeAdministracion(new CadenaDuena(Cadena));
        var lote = await consultas.ListarAsync(null, null, Ct);
        var eventos = lote.Eventos.Where(e => e.Tipo == "precio.cargar").OrderBy(e => e.Id).ToList();
        Assert.Equal(2, eventos.Count);

        var alta = FuenteAuditoriaAsistente.Mapear(eventos[0], new Dictionary<Guid, NombreResuelto>());
        Assert.Equal("Alta", alta.AccionEtiqueta);
        Assert.Equal("tabla_de_precios", alta.Tabla);
        Assert.Equal("openai/gpt-5", alta.RowPk);
        Assert.Equal(
            "Precio de openai / gpt-5: US$ 3 entrada · US$ 15 salida · US$ 0.3 caché por millón de tokens, desde 01/09/2026",
            alta.Resumen);

        var cambio = FuenteAuditoriaAsistente.Mapear(eventos[1], new Dictionary<Guid, NombreResuelto>());
        Assert.Equal("Cambio", cambio.AccionEtiqueta);
        Assert.Contains("desde 20/09/2026", cambio.Resumen, StringComparison.Ordinal);
        Assert.All(cambio.Cambios, c => Assert.False(c.Oculto));
        Assert.Contains(cambio.Cambios, c =>
            c.Campo == "entrada_por_millon" && c.ValorAnterior == "3" && c.ValorNuevo == "6"
            && c.EtiquetaCampo == "Entrada (US$ por millón)");
    }

    // --------------------------------------------------- 5.7 read: priced vs not

    [Fact]
    public async Task The_model_read_lists_the_price_in_force_and_omits_models_without_one()
    {
        await SembrarAsync(Ct);
        using var host = CrearHost();
        using var cliente = Admin(host);

        var antes = await cliente.GetFromJsonAsync<JsonElement>($"{Ruta}/modelo", Ct);
        Assert.Equal(0, antes.GetProperty("precios").GetArrayLength());

        _ = await Cargar(cliente, "openai", "gpt-5", 3m, 15m, 0.3m, "2026-01-01");
        _ = await Cargar(cliente, "openai", "gpt-5", 6m, 30m, 0.6m, "2026-02-01");
        // A scheduled price in the future is not "in force" yet.
        _ = await Cargar(cliente, "ollama", "qwen3.5:9b", 1m, 1m, 1m, "2999-01-01");

        var despues = await cliente.GetFromJsonAsync<JsonElement>($"{Ruta}/modelo", Ct);
        var precio = Assert.Single(despues.GetProperty("precios").EnumerateArray());
        Assert.Equal("openai", precio.GetProperty("empresa").GetString());
        Assert.Equal("gpt-5", precio.GetProperty("modelo").GetString());
        Assert.Equal(6m, precio.GetProperty("entradaPorMillon").GetDecimal());
        Assert.Equal(30m, precio.GetProperty("salidaPorMillon").GetDecimal());
        Assert.Equal(0.6m, precio.GetProperty("cachePorMillon").GetDecimal());
        Assert.Equal(2, precio.GetProperty("version").GetInt32());
        Assert.Equal("2026-02-01", precio.GetProperty("vigenteDesde").GetString());
    }

    // ------------------------------------------------------------------- support

    private async Task InsertarUsoAsync(DateTimeOffset cuando, string proveedor)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO asistente.registro_operativo
                (actor_id, ocurrido_en, carril, estado, llamadas_al_modelo, tokens_de_entrada,
                 tokens_de_salida, latencia_ms, hubo_reintento, truncado, proveedor)
            VALUES (@actor, @cuando, 'Sql', 'Respondida', 1, 1000000, 0, 10, false, false, @proveedor)
            """, conexion);
        comando.Parameters.AddWithValue("actor", Secretaria);
        comando.Parameters.AddWithValue("cuando", cuando);
        comando.Parameters.AddWithValue("proveedor", proveedor);
        await comando.ExecuteNonQueryAsync(Ct);
    }

    /// <summary>The same half-open lookup the readers use, straight against the table.</summary>
    private async Task<decimal?> EntradaVigenteEnAsync(string proveedor, string modelo, DateTimeOffset momento)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT precio_por_token_entrada FROM asistente.tabla_de_precios
             WHERE proveedor = @p AND modelo = @m AND vigente_desde <= @t
               AND (vigente_hasta IS NULL OR @t < vigente_hasta)
             ORDER BY vigente_desde DESC LIMIT 1
            """, conexion);
        comando.Parameters.AddWithValue("p", proveedor);
        comando.Parameters.AddWithValue("m", modelo);
        comando.Parameters.AddWithValue("t", momento);
        return await comando.ExecuteScalarAsync(Ct) as decimal?;
    }
}
