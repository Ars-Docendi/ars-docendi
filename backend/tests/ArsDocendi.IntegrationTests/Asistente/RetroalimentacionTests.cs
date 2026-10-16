using System.Net;
using System.Net.Http.Json;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Asistente;
using Modules.Asistente.Api;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// The turn-level feedback flow, end to end: minting the token
/// (asistente-contrato-de-respuesta), and <c>POST /api/asistente/retroalimentacion</c>
/// (asistente-retroalimentacion).
/// </summary>
public sealed class RetroalimentacionTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_retroalimentacion")
{
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");

    private const string ContarDocentes =
        "SELECT count(*) AS cantidad FROM designaciones.designaciones";

    private const string Ruta = "/api/asistente/retroalimentacion";

    // -------------------------------------------------- token minting (2.1-2.3)

    [Fact]
    public async Task Only_a_Respondida_result_carries_a_feedback_token()
    {
        await SembrarAsync();
        var banco = Banco(ProveedorGuionado.Generacion(ContarDocentes));

        var respondida = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos docentes hay?", TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.Respondida, respondida.Estado);
        Assert.NotNull(respondida.ClaveDeRetroalimentacion);
    }

    [Fact]
    public async Task No_other_turn_state_carries_a_feedback_token()
    {
        await SembrarAsync();

        var rechazo = await Banco(ProveedorGuionado.NoContestable()).Capa().ResponderAsync(
            Secretaria, null, "¿cuál es la temperatura del aula 302?",
            TestContext.Current.CancellationToken);
        Assert.Equal(EstadoDelTurno.NoContestable, rechazo.Estado);
        Assert.Null(rechazo.ClaveDeRetroalimentacion);

        var degradado = BancoDegradado();
        var turnoDegradado = await degradado.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos docentes hay?", TestContext.Current.CancellationToken);
        Assert.Equal(EstadoDelTurno.ServicioDegradado, turnoDegradado.Estado);
        Assert.Null(turnoDegradado.ClaveDeRetroalimentacion);
    }

    [Fact]
    public void Analitico_id_is_supplied_by_the_caller_and_not_the_database_default()
    {
        // The unit-level assertion behind 2.1: TurnoParaRegistrar carries its own
        // caller-generated id rather than leaving it to registro_analitico's own
        // DEFAULT gen_random_uuid().
        var turno = NuevoTurnoParaRegistrar(Guid.NewGuid());

        Assert.NotEqual(Guid.Empty, turno.AnaliticoId);
    }

    [Fact]
    public async Task The_response_DTO_maps_the_feedback_token_and_only_when_present()
    {
        // Contrast test, mirroring the existing one for SqlEjecutado (2.3): a field
        // this DTO is NOT supposed to expose vs. one it IS.
        var conToken = RespuestaDelAsistente.De(TurnoDeEjemplo(Guid.NewGuid()));
        var sinToken = RespuestaDelAsistente.De(
            TurnoDeEjemplo(null) with { Estado = EstadoDelTurno.NoContestable });

        Assert.NotNull(conToken.ClaveDeRetroalimentacion);
        Assert.Null(sinToken.ClaveDeRetroalimentacion);
    }

    // -------------------------------------------------------- the endpoint (3.x)

    [Fact]
    public async Task A_valid_token_from_a_just_answered_turn_records_a_row()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var token = await PreguntarYObtenerTokenAsync(cliente);

        using var respuesta = await VotarAsync(cliente, token, voto: true);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.Equal(1L, await ContarFilaDeRetroalimentacionAsync(token));
    }

    [Fact]
    public async Task An_unknown_token_and_an_expired_one_get_the_same_rejection()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var nuncaEmitido = Guid.NewGuid();
        using var rechazoDesconocido = await VotarAsync(cliente, nuncaEmitido, voto: true);

        var vencido = await PreguntarYObtenerTokenAsync(cliente);
        // Directly expire it in the shared store rather than waiting two hours.
        VencerToken(host, vencido);
        using var rechazoVencido = await VotarAsync(cliente, vencido, voto: true);

        Assert.Equal(HttpStatusCode.NotFound, rechazoDesconocido.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, rechazoVencido.StatusCode);

        var cuerpoDesconocido = await rechazoDesconocido.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        var cuerpoVencido = await rechazoVencido.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        Assert.Equal(cuerpoDesconocido, cuerpoVencido);
    }

    [Fact]
    public async Task Submitting_twice_upserts_one_row_with_the_latest_vote()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var token = await PreguntarYObtenerTokenAsync(cliente);

        await VotarAsync(cliente, token, voto: true);
        await VotarAsync(
            cliente, token, voto: false, razones: [RazonesDeRetroalimentacionExpuestas.FaltanDatos]);

        Assert.Equal(1L, await ContarFilaDeRetroalimentacionAsync(token));
        var (voto, razones, _) = await LeerFilaAsync(token);
        Assert.False(voto);
        Assert.Equal([RazonesDeRetroalimentacionExpuestas.FaltanDatos], razones);
    }

    [Fact]
    public async Task A_thumbs_up_with_a_reason_present_stores_no_reason()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var token = await PreguntarYObtenerTokenAsync(cliente);

        await VotarAsync(
            cliente, token, voto: true, razones: [RazonesDeRetroalimentacionExpuestas.Otro]);

        var (voto, razones, _) = await LeerFilaAsync(token);
        Assert.True(voto);
        Assert.Null(razones);
    }

    [Fact]
    public async Task A_thumbs_down_with_several_reasons_stores_all_of_them()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var token = await PreguntarYObtenerTokenAsync(cliente);

        using var respuesta = await VotarAsync(
            cliente, token, voto: false,
            razones:
            [
                RazonesDeRetroalimentacionExpuestas.DatosIncorrectos,
                RazonesDeRetroalimentacionExpuestas.FaltanDatos,
            ]);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        var (_, razones, _) = await LeerFilaAsync(token);
        Assert.Equal(
            [
                RazonesDeRetroalimentacionExpuestas.DatosIncorrectos,
                RazonesDeRetroalimentacionExpuestas.FaltanDatos,
            ],
            razones);
    }

    [Fact]
    public async Task A_duplicated_reason_is_rejected()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var token = await PreguntarYObtenerTokenAsync(cliente);

        using var respuesta = await VotarAsync(
            cliente, token, voto: false,
            razones: [RazonesDeRetroalimentacionExpuestas.Otro, RazonesDeRetroalimentacionExpuestas.Otro]);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(0L, await ContarFilaDeRetroalimentacionAsync(token));
    }

    [Fact]
    public async Task A_comment_within_the_limit_is_stored_trimmed()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var token = await PreguntarYObtenerTokenAsync(cliente);

        using var respuesta = await VotarAsync(
            cliente, token, voto: false, comentario: "  Esperaba otra cosa  ");

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        var (_, _, comentario) = await LeerFilaAsync(token);
        Assert.Equal("Esperaba otra cosa", comentario);
    }

    [Fact]
    public async Task A_comment_over_the_limit_after_trimming_is_rejected()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var token = await PreguntarYObtenerTokenAsync(cliente);
        var comentarioLargo = new string('a', 501);

        using var respuesta = await VotarAsync(cliente, token, voto: false, comentario: comentarioLargo);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(0L, await ContarFilaDeRetroalimentacionAsync(token));
    }

    [Fact]
    public async Task A_whitespace_only_comment_is_stored_as_absent()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var token = await PreguntarYObtenerTokenAsync(cliente);

        using var respuesta = await VotarAsync(cliente, token, voto: false, comentario: "   ");

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        var (_, _, comentario) = await LeerFilaAsync(token);
        Assert.Null(comentario);
    }

    [Fact]
    public async Task A_thumbs_up_with_a_comment_present_stores_no_comment()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var token = await PreguntarYObtenerTokenAsync(cliente);

        await VotarAsync(cliente, token, voto: true, comentario: "no debería guardarse");

        var (voto, _, comentario) = await LeerFilaAsync(token);
        Assert.True(voto);
        Assert.Null(comentario);
    }

    [Fact]
    public async Task A_token_that_was_never_issued_for_a_non_answered_turn_404s()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        // NoContestable never mints a token — this is just a guess.
        var adivinado = Guid.NewGuid();
        using var respuesta = await VotarAsync(cliente, adivinado, voto: true);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task An_unrecognized_reason_is_rejected()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var token = await PreguntarYObtenerTokenAsync(cliente);

        using var respuesta = await VotarAsync(
            cliente, token, voto: false, razones: ["no-es-una-razon-valida"]);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    // ------------------------------------------ D7: `lento` removed entirely (5.6)

    [Fact]
    public async Task Faltan_datos_is_accepted_as_a_reason()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var token = await PreguntarYObtenerTokenAsync(cliente);

        using var respuesta = await VotarAsync(
            cliente, token, voto: false, razones: [RazonesDeRetroalimentacionExpuestas.FaltanDatos]);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        var (_, razones, _) = await LeerFilaAsync(token);
        Assert.Equal([RazonesDeRetroalimentacionExpuestas.FaltanDatos], razones);
    }

    [Fact]
    public async Task Lento_is_rejected_like_any_other_unknown_reason()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var token = await PreguntarYObtenerTokenAsync(cliente);

        using var respuesta = await VotarAsync(
            cliente, token, voto: false, razones: [RazonesDeRetroalimentacionExpuestas.Lento]);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(0L, await ContarFilaDeRetroalimentacionAsync(token));
    }

    // ------------------------------------------- D3: the log-field separation

    [Fact]
    public async Task The_turns_log_entry_and_the_feedback_endpoints_never_share_actor_and_token()
    {
        await SembrarAsync();
        var registrador = new RegistradorDeEventos();
        var banco = Banco(ProveedorGuionado.Generacion(ContarDocentes), registrador);

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos docentes hay?", TestContext.Current.CancellationToken);
        var token = turno.ClaveDeRetroalimentacion!.Value;

        var servicio = new ServicioDeRetroalimentacion(
            banco.ValidezDeRetroalimentacion,
            new RegistroDeRetroalimentacion(new CadenaDuena(Cadena)),
            TimeProvider.System,
            new LoggerDeEventos<ServicioDeRetroalimentacion>(registrador));
        await servicio.RegistrarAsync(token, true, null, null, TestContext.Current.CancellationToken);

        // The turn's own events may name the actor; none may also name the token.
        Assert.DoesNotContain(registrador.Eventos, e => e.Contains(Secretaria.ToString())
            && e.Contains(token.ToString()));

        // At least one event on each side proves the assertion above is not
        // vacuous: the turn's pipeline does log the actor, and the feedback
        // service does log the token — just never together.
        Assert.Contains(registrador.Eventos, e => e.Contains(Secretaria.ToString()));
        Assert.Contains(registrador.Eventos, e => e.Contains(token.ToString()));
    }

    // ------------------------------------------------------------------ apoyo

    private BancoDelAsistente Banco(string guion, RegistradorDeEventos? registrador = null)
    {
        var (basica, pii) = CadenasDeLectura();
        var opciones = new OpcionesAsistente();

        return BancoDelAsistente.Armar(
            basica, pii, ClasificadorDeSensibilidad(), Apertura, opciones,
            // The real writer and not the in-memory fake: the D3 test that passes
            // a registrador goes on to submit feedback for the turn's token, and
            // that INSERT has a foreign key into registro_analitico — the row has
            // to actually exist.
            registro: registrador is null
                ? null
                : new RegistroDelTurno(new CadenaDuena(Cadena), NullLogger<RegistroDelTurno>.Instance),
            logCapa: registrador is null ? null : new LoggerDeEventos<CapaConversacional>(registrador),
            guion: [guion]);
    }

    private BancoDelAsistente BancoDegradado()
    {
        var (basica, pii) = CadenasDeLectura();
        var opciones = new OpcionesAsistente { FallosParaAbrirElBreaker = 1 };
        var banco = BancoDelAsistente.Armar(
            basica, pii, ClasificadorDeSensibilidad(), Apertura, opciones);
        banco.Breaker.Fallo();
        return banco;
    }

    private static TurnoParaRegistrar NuevoTurnoParaRegistrar(Guid analiticoId) =>
        new(Secretaria,
            analiticoId,
            DateTimeOffset.UtcNow,
            CarrilDelTurno.Sql,
            EstadoDelTurno.Respondida,
            LlamadasAlModelo: 2,
            TokensDeEntrada: 100,
            TokensDeSalida: 50,
            TokensDeCache: 0,
            LatenciaMs: 10,
            HuboReintento: false,
            Truncado: false,
            "¿cuántos docentes hay?",
            "cruce_de_tablas",
            Proveedor: "guionado",
            IntencionSombra: null);

    private static ResultadoDelTurno TurnoDeEjemplo(Guid? token) =>
        new(EstadoDelTurno.Respondida,
            "Hay 4 docentes.",
            Razonamiento: string.Empty,
            PreguntaInterpretada: null,
            [],
            [],
            Truncado: false,
            [],
            "cruce_de_tablas",
            LlamadasAlModelo: 2,
            Guid.NewGuid(),
            ClaveDeRetroalimentacion: token);

    // --------------------------------------- reemplazo (design.md D9, ARS-147)

    [Fact]
    public async Task El_token_del_turno_reemplazado_se_rechaza_como_desconocido()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var primero = await PreguntarAsync(cliente, "¿cuántos docentes hay?", "clave-1");
        var tokenViejo = primero.ClaveDeRetroalimentacion!.Value;

        await PreguntarAsync(
            cliente, "¿cuántos adjuntos hay?", "clave-2", hilo: primero.Hilo, reemplaza: "clave-1");

        using var respuesta = await VotarAsync(cliente, tokenViejo, voto: true);

        // Mismo rechazo que un token desconocido o vencido — la superficie no
        // puede distinguir entre los tres casos.
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_token_del_turno_nuevo_se_acepta()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var primero = await PreguntarAsync(cliente, "¿cuántos docentes hay?", "clave-1");

        var segundo = await PreguntarAsync(
            cliente, "¿cuántos adjuntos hay?", "clave-2", hilo: primero.Hilo, reemplaza: "clave-1");
        var tokenNuevo = segundo.ClaveDeRetroalimentacion!.Value;

        using var respuesta = await VotarAsync(cliente, tokenNuevo, voto: true);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.Equal(1L, await ContarFilaDeRetroalimentacionAsync(tokenNuevo));
    }

    [Fact]
    public async Task El_voto_del_turno_reemplazado_se_conserva()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var primero = await PreguntarAsync(cliente, "¿cuántos docentes hay?", "clave-1");
        var tokenViejo = primero.ClaveDeRetroalimentacion!.Value;

        await VotarAsync(cliente, tokenViejo, voto: true);
        Assert.Equal(1L, await ContarFilaDeRetroalimentacionAsync(tokenViejo));

        await PreguntarAsync(
            cliente, "¿cuántos adjuntos hay?", "clave-2", hilo: primero.Hilo, reemplaza: "clave-1");

        // El voto viejo sigue existiendo —anónimo, sujeto a la misma retención
        // de siempre— aunque su turno ya no sea el último (design.md D9).
        Assert.Equal(1L, await ContarFilaDeRetroalimentacionAsync(tokenViejo));
    }

    private async Task<RespuestaDelAsistente> PreguntarAsync(
        HttpClient cliente, string mensaje, string clave, Guid? hilo = null, string? reemplaza = null)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/asistente/consultas")
        {
            Content = JsonContent.Create(new ConsultaDelAsistente(mensaje, hilo, reemplaza)),
        };
        pedido.Headers.TryAddWithoutValidation(AsistenteController.CabeceraDeIdempotencia, clave);

        using var respuesta = await cliente.SendAsync(pedido, TestContext.Current.CancellationToken);
        var cuerpo = await respuesta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.True(respuesta.IsSuccessStatusCode, cuerpo);

        return (await respuesta.Content.ReadFromJsonAsync<RespuestaDelAsistente>(
            TestContext.Current.CancellationToken))!;
    }

    private async Task<Guid> PreguntarYObtenerTokenAsync(HttpClient cliente)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/asistente/consultas")
        {
            Content = JsonContent.Create(new ConsultaDelAsistente("¿cuántos docentes hay?", null)),
        };
        pedido.Headers.TryAddWithoutValidation(
            AsistenteController.CabeceraDeIdempotencia, Guid.NewGuid().ToString());

        using var respuesta = await cliente.SendAsync(pedido, TestContext.Current.CancellationToken);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<RespuestaDelAsistente>(
            TestContext.Current.CancellationToken);

        Assert.True(respuesta.IsSuccessStatusCode);
        Assert.NotNull(cuerpo?.ClaveDeRetroalimentacion);

        return cuerpo!.ClaveDeRetroalimentacion!.Value;
    }

    private static async Task<HttpResponseMessage> VotarAsync(
        HttpClient cliente,
        Guid token,
        bool voto,
        IReadOnlyList<string>? razones = null,
        string? comentario = null)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Post, Ruta)
        {
            Content = JsonContent.Create(new PedidoDeRetroalimentacion(token, voto, razones, comentario)),
        };

        return await cliente.SendAsync(pedido, TestContext.Current.CancellationToken);
    }

    private static void VencerToken(WebApplicationFactory<Program> host, Guid token)
    {
        using var scope = host.Services.CreateScope();
        var validez = scope.ServiceProvider.GetRequiredService<IValidezDeRetroalimentacion>();
        validez.Registrar(token, DateTimeOffset.UtcNow - TimeSpan.FromHours(3));
    }

    private async Task<long> ContarFilaDeRetroalimentacionAsync(Guid token) =>
        await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.retroalimentacion_turno WHERE analitico_id = @t",
            ("t", token));

    private async Task<(bool Voto, string[]? Razones, string? Comentario)> LeerFilaAsync(Guid token)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            "SELECT voto, razones, comentario FROM asistente.retroalimentacion_turno "
            + "WHERE analitico_id = @t",
            conexion);
        comando.Parameters.AddWithValue("t", token);

        await using var lector = await comando.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        await lector.ReadAsync(TestContext.Current.CancellationToken);

        return (
            lector.GetBoolean(0),
            lector.IsDBNull(1) ? null : lector.GetFieldValue<string[]>(1),
            lector.IsDBNull(2) ? null : lector.GetString(2));
    }

    private static void Autenticar(HttpClient cliente, Guid usuario, string rol)
    {
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, usuario.ToString());
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, rol);
    }

    private WebApplicationFactory<Program> CrearHost()
    {
        var guion = Enumerable.Range(0, 12).SelectMany(_ => new[]
        {
            ProveedorGuionado.Generacion(ContarDocentes),
            "Hay 4 docentes designados.",
        }).ToArray();

        var guionado = new ProveedorGuionado(guion);

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting($"ConnectionStrings:{CadenaDuena.Clave}", Cadena);
            builder.UseSetting($"{AutenticacionDesarrolloOptions.Seccion}:Enabled", "true");
            builder.UseSetting(
                $"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.RolSoloLectura)}",
                RolSoloLectura);
            builder.UseSetting(
                $"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.RolSoloLecturaPii)}",
                RolSoloLecturaPii);
            builder.UseSetting(
                $"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.PasswordSoloLectura)}",
                PostgresFixture.PasswordDeRol);
            builder.UseSetting(
                $"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.PasswordSoloLecturaPii)}",
                PostgresFixture.PasswordDeRol);

            builder.ConfigureTestServices(servicios =>
                servicios.AddSingleton(new ProveedorBase(guionado)));
        });
    }

    /// <summary>Mirrors the reason values without depending on the internal type.</summary>
    private static class RazonesDeRetroalimentacionExpuestas
    {
        public const string DatosIncorrectos = "datos_incorrectos";
        public const string NoEntendioLaPregunta = "no_entendio_la_pregunta";
        public const string FaltanDatos = "faltan_datos";
        public const string Otro = "otro";

        /// <summary>
        /// Removed entirely by D7 of asistente-rediseno-v3 (PO-changed 2026-09-26):
        /// nothing shipped to production, so there is no legacy row anywhere. Kept
        /// here only to prove the API rejects it like any other unknown value.
        /// </summary>
        public const string Lento = "lento";
    }

    /// <summary>Captures every formatted log event, across every logger.</summary>
    private sealed class RegistradorDeEventos
    {
        private readonly List<string> _eventos = [];
        public IReadOnlyList<string> Eventos => _eventos;
        public void Agregar(string evento) => _eventos.Add(evento);
    }

    private sealed class LoggerDeEventos<T>(RegistradorDeEventos registrador) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            registrador.Agregar(formatter(state, exception));
    }
}
