using System.Net;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Every failure or empty path of a turn records its cause on the analytic record
/// (asistente-metricas-de-calidad, tasks 2.6, 2.7, 2.9 and 2.10).
/// </summary>
public sealed class CausasDelTurnoTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_causas")
{
    private static readonly DateTimeOffset Ancla = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    private static readonly Guid Coordinador = Guid.Parse("a0000000-0000-4000-8000-000000000003");

    private const string ContarDocentes =
        "SELECT count(*) AS cantidad FROM designaciones.designaciones";

    private const string Pregunta = "¿cuántos docentes están designados?";

    // -------------------------------------------------------------- provider

    [Fact]
    public async Task Provider_unavailable_is_recorded_when_the_availability_precheck_finds_the_breaker_open()
    {
        await SembrarAsync();
        var banco = Banco(new OpcionesAsistente { FallosParaAbrirElBreaker = 1 });
        banco.Breaker.Fallo();

        await ResponderAsync(banco);

        await AfirmarAsync(CausaDelTurno.ProveedorNoDisponible);
    }

    [Fact]
    public async Task Provider_unavailable_is_recorded_when_the_breaker_cuts_a_call_mid_turn()
    {
        await SembrarAsync();
        var banco = Banco(proveedor: new ProveedorGuionado { Falla = new ProveedorNoDisponible() });

        await ResponderAsync(banco);

        await AfirmarAsync(CausaDelTurno.ProveedorNoDisponible);
    }

    [Fact]
    public async Task Saturated_is_recorded_and_the_breaker_stays_closed()
    {
        await SembrarAsync();
        var banco = Banco(proveedor: new ProveedorGuionado { Falla = new ProveedorSaturado(TimeSpan.Zero) });

        await ResponderAsync(banco);

        await AfirmarAsync(CausaDelTurno.ProveedorSaturado);
        Assert.Equal(EstadoDelBreaker.Cerrado, banco.Breaker.Estado);
    }

    [Fact]
    public async Task Provider_timeout_is_recorded()
    {
        await SembrarAsync();
        var banco = Banco(proveedor: new ProveedorGuionado
        {
            Falla = new TimeoutDelProveedor(TimeSpan.FromSeconds(1)),
        });

        await ResponderAsync(banco);

        await AfirmarAsync(CausaDelTurno.TimeoutProveedor);
    }

    [Fact]
    public async Task A_client_side_cancellation_not_from_the_request_is_recorded_as_a_provider_timeout()
    {
        await SembrarAsync();
        var banco = Banco(proveedor: new ProveedorGuionado { Falla = new TaskCanceledException("http timeout") });

        await ResponderAsync(banco);

        await AfirmarAsync(CausaDelTurno.TimeoutProveedor);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task A_rejected_credential_is_recorded(HttpStatusCode estado)
    {
        await SembrarAsync();
        var banco = Banco(proveedor: new ProveedorGuionado
        {
            Falla = new HttpRequestException("rechazada", null, estado),
        });

        await ResponderAsync(banco);

        await AfirmarAsync(CausaDelTurno.CredencialRechazada);
    }

    [Fact]
    public async Task A_model_not_found_is_recorded()
    {
        await SembrarAsync();
        var banco = Banco(proveedor: new ProveedorGuionado
        {
            Falla = new HttpRequestException("no existe", null, HttpStatusCode.NotFound),
        });

        await ResponderAsync(banco);

        await AfirmarAsync(CausaDelTurno.ModeloNoEncontrado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task Any_other_http_failure_of_the_provider_is_recorded_as_a_provider_error(HttpStatusCode? estado)
    {
        await SembrarAsync();
        var banco = Banco(proveedor: new ProveedorGuionado
        {
            Falla = new HttpRequestException("falla", null, estado),
        });

        await ResponderAsync(banco);

        await AfirmarAsync(CausaDelTurno.ErrorDelProveedor);
    }

    [Fact]
    public async Task The_call_ceiling_is_recorded()
    {
        await SembrarAsync();
        var banco = Banco(
            new OpcionesAsistente { MaximoDeLlamadasPorTurno = 1 },
            guion: [ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes."]);

        await ResponderAsync(banco);

        await AfirmarAsync(CausaDelTurno.TechoDeLlamadas);
    }

    [Fact]
    public async Task An_expired_turn_budget_is_recorded()
    {
        await SembrarAsync();
        var reloj = new RelojFijo(Ancla);
        var banco = Banco(
            new OpcionesAsistente { PresupuestoDelTurnoSegundos = 5, TimeoutDeLlamadaSegundos = 20 },
            reloj: reloj,
            proveedor: new ProveedorGuionado(ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.")
            {
                Antes = () => reloj.Avanzar(TimeSpan.FromSeconds(6)),
            });

        await ResponderAsync(banco);

        await AfirmarAsync(CausaDelTurno.PresupuestoDelTurno);
    }

    [Fact]
    public async Task A_generation_cut_by_the_token_ceiling_is_recorded()
    {
        await SembrarAsync();
        var banco = Banco(proveedor: new ProveedorGuionado(
            """{"es_contestable": true, "sql": "SELECT count(*) AS cantidad FROM designaci""")
        {
            SeQuedaSinTokens = true,
        });

        await ResponderAsync(banco);

        await AfirmarAsync(CausaDelTurno.GeneracionCortada);
    }

    [Fact]
    public async Task An_unintelligible_generation_is_recorded()
    {
        await SembrarAsync();
        var banco = Banco(proveedor: new ProveedorGuionado("esto no es un objeto JSON"));

        await ResponderAsync(banco);

        await AfirmarAsync(CausaDelTurno.RespuestaIninteligible);
    }

    // ----------------------------------------------------------------- query

    [Fact]
    public async Task A_validator_rejection_is_recorded()
    {
        await SembrarAsync();
        var banco = Banco(proveedor: new ProveedorGuionado(
            ProveedorGuionado.Generacion("DELETE FROM designaciones.designaciones")));

        await ResponderAsync(banco);

        await AfirmarAsync(CausaDelTurno.ValidadorRechazo);
    }

    [Fact]
    public async Task An_engine_refusal_for_lack_of_privilege_is_recorded()
    {
        await SembrarAsync();
        var banco = Banco(proveedor: new ProveedorGuionado(
            ProveedorGuionado.Generacion("SELECT telefono FROM identity.personas")));

        // The coordinator does not see personal data: the engine refuses the column (42501).
        await ResponderAsync(banco, Coordinador);

        await AfirmarAsync(CausaDelTurno.SinPrivilegioDeDatos);
    }

    [Fact]
    public async Task Another_engine_rejection_is_recorded_as_an_engine_error()
    {
        await SembrarAsync();
        var banco = Banco(proveedor: new ProveedorGuionado(
            ProveedorGuionado.Generacion("SELECT columna_inexistente FROM designaciones.pedidos")));

        await ResponderAsync(banco);

        await AfirmarAsync(CausaDelTurno.ErrorDelMotor);
    }

    [Fact]
    public async Task A_statement_timeout_is_recorded_apart_from_an_engine_error()
    {
        await SembrarAsync();
        var banco = Banco(
            new OpcionesAsistente { TimeoutDeSentenciaMs = 50 },
            proveedor: new ProveedorGuionado(ProveedorGuionado.Generacion(
                "SELECT count(*) AS cantidad FROM generate_series(1, 2000000000)")));

        await ResponderAsync(banco);

        await AfirmarAsync(CausaDelTurno.TimeoutDeSentencia);
    }

    [Fact]
    public async Task An_unhandled_exception_is_recorded_as_a_fallo()
    {
        await SembrarAsync();
        var banco = Banco(proveedor: new ProveedorGuionado(
            ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.")
        {
            Antes = () => throw new InvalidOperationException("se cayó el turno"),
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => ResponderAsync(banco));

        await AfirmarAsync(CausaDelTurno.Excepcion);
    }

    // ---------------------------------------------------------------- signal

    [Fact]
    public async Task An_empty_result_is_recorded_as_answered_with_the_empty_result_cause()
    {
        await SembrarAsync();
        var banco = Banco(proveedor: new ProveedorGuionado(ProveedorGuionado.Generacion(
            "SELECT id FROM designaciones.designaciones WHERE false")));

        var turno = await ResponderAsync(banco);

        Assert.Equal(EstadoDelTurno.Respondida, turno.Estado);
        Assert.Equal(CausaDelTurno.ResultadoVacio, turno.Causa);
        await AfirmarAsync(CausaDelTurno.ResultadoVacio);
    }

    // ---------------------------------------------------------------- policy

    [Fact]
    public async Task An_exhausted_quota_is_recorded()
    {
        await SembrarAsync();
        var banco = Banco(cupoDiario: 1, guion: [ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes."]);

        await ResponderAsync(banco);
        await BorrarRegistrosAsync();
        await ResponderAsync(banco);

        await AfirmarAsync(CausaDelTurno.CupoAgotado);
    }

    [Fact]
    public async Task An_exhausted_organization_cap_is_recorded()
    {
        await SembrarAsync();
        var banco = Banco(
            cupoDiario: 100,
            topeOrganizacional: 1,
            guion: [ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes."]);

        await ResponderAsync(banco);
        await BorrarRegistrosAsync();
        await ResponderAsync(banco);

        await AfirmarAsync(CausaDelTurno.TopeOrganizacional);
    }

    [Fact]
    public async Task Maintenance_mode_is_recorded()
    {
        await SembrarAsync();
        var mantenimiento = new DisponibilidadDelModuloFalsa();
        await mantenimiento.ActivarAsync(Secretaria, "mantenimiento programado", default);
        var banco = Banco(mantenimiento: mantenimiento);

        await ResponderAsync(banco);

        await AfirmarAsync(CausaDelTurno.Mantenimiento);
    }

    [Fact]
    public async Task Revoked_access_is_recorded()
    {
        await SembrarAsync();
        var banco = Banco(acceso: new AccesoAlAsistenteFalso(tieneAcceso: false));

        await ResponderAsync(banco);

        await AfirmarAsync(CausaDelTurno.AccesoRevocado);
    }

    [Fact]
    public async Task A_refused_turn_lock_is_recorded()
    {
        await SembrarAsync();
        var banco = Banco();
        await using var tomado = await banco.CandadoDelTurno.IntentarAsync(Secretaria, TestContext.Current.CancellationToken);
        Assert.NotNull(tomado);

        await ResponderAsync(banco);

        await AfirmarAsync(CausaDelTurno.TurnoConcurrente);
    }

    // ------------------------------------------------- what is not a cause

    [Fact]
    public async Task A_model_declination_records_no_cause_and_keeps_its_reason_on_the_operational_record()
    {
        await SembrarAsync();
        var banco = Banco(proveedor: new ProveedorGuionado(
            ProveedorGuionado.NoContestableConMotivo("fuera_de_tema")));

        var turno = await ResponderAsync(banco, pregunta: "¿cuál es la temperatura del aula 302?");

        Assert.Equal(EstadoDelTurno.NoContestable, turno.Estado);
        Assert.Null(turno.Causa);
        var fila = await LeerFilaAsync();
        Assert.Null(fila.Causa);
        Assert.Equal("NoContestable", fila.Estado);
        Assert.Equal(
            "fuera_de_tema",
            await EscalarAsync<string>("SELECT motivo_rechazo FROM asistente.registro_operativo"));
    }

    [Fact]
    public async Task A_normal_answer_records_no_cause()
    {
        await SembrarAsync();
        var banco = Banco(guion: [ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes."]);

        await ResponderAsync(banco);

        var fila = await LeerFilaAsync();
        Assert.Null(fila.Causa);
        Assert.Equal("Respondida", fila.Estado);
    }

    // ------------------------------------------------------- the model name

    [Fact]
    public async Task The_model_is_the_adapter_name_of_the_turn_and_the_operational_provider_is_unchanged()
    {
        await SembrarAsync();
        var banco = Banco(guion: [ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes."]);

        await ResponderAsync(banco);

        var fila = await LeerFilaAsync();
        Assert.Equal(banco.Proveedor.Nombre, fila.Modelo);
        Assert.Equal(
            banco.Proveedor.Nombre,
            await EscalarAsync<string>("SELECT proveedor FROM asistente.registro_operativo"));
    }

    [Fact]
    public async Task The_model_is_recorded_on_a_turn_that_never_called_a_model()
    {
        await SembrarAsync();
        var banco = Banco(acceso: new AccesoAlAsistenteFalso(tieneAcceso: false));

        await ResponderAsync(banco);

        Assert.Equal(0, banco.Proveedor.Llamadas);
        Assert.Equal(banco.Proveedor.Nombre, (await LeerFilaAsync()).Modelo);
    }

    // ------------------------------------------- cause agrees with its state

    [Fact]
    public async Task The_check_constraint_lists_exactly_the_codes_of_the_enum()
    {
        var definicion = await EscalarAsync<string>(
            """
            SELECT pg_get_constraintdef(oid)
              FROM pg_constraint
             WHERE conname = 'registro_analitico_causa_valida'
            """);

        var enBase = System.Text.RegularExpressions.Regex.Matches(definicion, "'([a-z_]+)'")
            .Select(m => m.Groups[1].Value)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var enCodigo = CausasDelTurno.Todas
            .Select(CausasDelTurno.ValorDeCable)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(22, enCodigo.Length);
        Assert.Equal(enCodigo, enBase);
    }

    [Fact]
    public void Every_cause_agrees_with_the_state_of_its_family()
    {
        foreach (var causa in CausasDelTurno.Todas)
        {
            var estado = CausasDelTurno.EstadoEsperado(causa);

            // Policy denials are always degraded, the signal is the only cause on an
            // answered turn, and an unhandled exception is the only one recorded as Fallo.
            if (CausasDelTurno.Familia(causa) == FamiliaDeCausa.Politica)
            {
                Assert.Equal(EstadoDelTurno.ServicioDegradado, estado);
            }

            Assert.Equal(causa == CausaDelTurno.ResultadoVacio, estado == EstadoDelTurno.Respondida);
            Assert.Equal(causa == CausaDelTurno.Excepcion, estado == EstadoDelTurno.Fallo);
            Assert.Equal(
                causa == CausaDelTurno.ResultadoVacio,
                CausasDelTurno.Familia(causa) == FamiliaDeCausa.Senal);
        }

        Assert.Equal(5, CausasDelTurno.Todas.Count(c => CausasDelTurno.Familia(c) == FamiliaDeCausa.Politica));
    }

    [Theory]
    [InlineData(null, CausaDelTurno.ErrorDelProveedor)]
    [InlineData(HttpStatusCode.Unauthorized, CausaDelTurno.CredencialRechazada)]
    [InlineData(HttpStatusCode.Forbidden, CausaDelTurno.CredencialRechazada)]
    [InlineData(HttpStatusCode.NotFound, CausaDelTurno.ModeloNoEncontrado)]
    [InlineData(HttpStatusCode.BadGateway, CausaDelTurno.ErrorDelProveedor)]
    public void An_http_status_maps_to_its_cause(HttpStatusCode? estado, CausaDelTurno esperada) =>
        Assert.Equal(esperada, CausasDelTurno.DeHttp(estado));

    [Theory]
    [InlineData("57014", CausaDelTurno.TimeoutDeSentencia)]
    [InlineData("42703", CausaDelTurno.ErrorDelMotor)]
    [InlineData(null, CausaDelTurno.ErrorDelMotor)]
    public void An_engine_state_maps_to_its_cause(string? sqlState, CausaDelTurno esperada) =>
        Assert.Equal(esperada, CausasDelTurno.DeMotor(sqlState));

    // ----------------------------------------------------------------- apoyo

    private BancoDelAsistente Banco(
        OpcionesAsistente? configuracion = null,
        ProveedorGuionado? proveedor = null,
        RelojFijo? reloj = null,
        int cupoDiario = 0,
        decimal topeOrganizacional = 0,
        DisponibilidadDelModuloFalsa? mantenimiento = null,
        IAccesoAlAsistente? acceso = null,
        params string[] guion)
    {
        var (basica, pii) = CadenasDeLectura();

        return BancoDelAsistente.Armar(
            basica,
            pii,
            ClasificadorDeSensibilidad(),
            Apertura,
            configuracion ?? new OpcionesAsistente(),
            reloj: reloj ?? new RelojFijo(Ancla),
            proveedor: proveedor,
            registro: new RegistroDelTurno(new CadenaDuena(Cadena), NullLogger<RegistroDelTurno>.Instance),
            cupoDiario: cupoDiario,
            topeOrganizacional: topeOrganizacional,
            disponibilidadDelModuloFalsa: mantenimiento,
            acceso: acceso,
            guion: guion);
    }

    private static Task<ResultadoDelTurno> ResponderAsync(
        BancoDelAsistente banco, Guid? actor = null, string pregunta = Pregunta) =>
        banco.Capa().ResponderAsync(
            actor ?? Secretaria, null, pregunta, TestContext.Current.CancellationToken);

    private Task BorrarRegistrosAsync() => EjecutarAsync(
        "DELETE FROM asistente.registro_analitico; DELETE FROM asistente.registro_operativo");

    private async Task AfirmarAsync(CausaDelTurno causa)
    {
        var fila = await LeerFilaAsync();

        Assert.Equal(CausasDelTurno.ValorDeCable(causa), fila.Causa);

        // The cause never changes the state: it must be the one the design table gives.
        Assert.Equal(CausasDelTurno.EstadoEsperado(causa).ToString(), fila.Estado);
    }

    private async Task<(string? Causa, string Estado, string? Modelo, bool Reemplazado)> LeerFilaAsync()
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            "SELECT causa, estado, modelo, reemplazado FROM asistente.registro_analitico", conexion);
        await using var lector = await comando.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        Assert.True(await lector.ReadAsync(TestContext.Current.CancellationToken), "No se registró el turno.");
        var fila = (
            lector.IsDBNull(0) ? null : lector.GetString(0),
            lector.GetString(1),
            lector.IsDBNull(2) ? null : lector.GetString(2),
            lector.GetBoolean(3));
        Assert.False(await lector.ReadAsync(TestContext.Current.CancellationToken), "Se registró más de un turno.");

        return fila;
    }
}
