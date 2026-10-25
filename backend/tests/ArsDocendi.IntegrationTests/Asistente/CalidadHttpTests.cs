using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Modules.Asistente;
using Modules.Asistente.Application;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// The quality endpoints over real HTTP and a real database
/// (asistente-metricas-de-calidad, section 3).
/// </summary>
/// <remarks>
/// The clock is fixed so a period boundary is the same on every run. Turns are seeded straight
/// into the analytic record and the vote table, because the readers only ever see those two.
/// </remarks>
public sealed class CalidadHttpTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_calidad_http")
{
    private const string Ruta = "/api/asistente/administracion/calidad";
    private const string ModeloA = "ollama/qwen3.5:9b";
    private const string ModeloB = "anthropic/claude-sonnet-5";
    private const string ComentarioSecreto = "COMENTARIO-SECRETO-QUE-NO-DEBE-SALIR";

    // sys_admin holds asistente.administrar by default and asistente.revisar_calidad never.
    private static readonly Guid Sistemas = Guid.Parse("a0000000-0000-4000-8000-000000000007");
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");

    private static readonly DateTimeOffset Ahora = new(2027, 6, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(Ahora.UtcDateTime);

    // ------------------------------------------------------------ the aggregates

    [Fact]
    public async Task Los_agregados_se_calculan_sobre_el_conjunto_sembrado()
    {
        await SembrarAsync(TestContext.Current.CancellationToken);
        await SembrarConjuntoAsync();

        var json = await ObtenerJsonAsync($"{Ruta}?periodo=30");

        // Votes: 5, of which 3 negative. Turns attended: 8 + 3; the 2 quota refusals are outside.
        var resumen = json["resumen"]!;
        Assert.Equal(11, (int)resumen["turnos"]!);
        Assert.Equal(5, (int)resumen["votos"]!);
        Assert.Equal(3, (int)resumen["negativos"]!);
        Assert.Equal(0.6, (double)resumen["tasaNegativa"]!, 6);

        // Coverage is votes over answered turns: 5 / 8.
        Assert.Equal(8, (int)resumen["respondidas"]!);
        Assert.Equal(0.625, (double)resumen["cobertura"]!, 6);

        // With problems, once per turn: three negatives (one of them also replaced), one more
        // replacement, and three failures = 7 distinct turns of 11.
        Assert.Equal(7, (int)resumen["conProblemas"]!);
        Assert.Equal(7d / 11d, (double)resumen["tasaConProblemas"]!, 6);

        // Failures: a timeout, an unclassified one and a validator rejection. The empty
        // result is a signal and is not counted.
        Assert.Equal(3, (int)resumen["fallosDetectables"]!);

        var motivos = Conteos(json["negativosPorMotivo"]!);
        Assert.Equal(1, motivos["datos_incorrectos"]);
        Assert.Equal(1, motivos["faltan_datos"]);
        Assert.Equal(1, motivos["otro"]);
        Assert.Equal(0, motivos["no_entendio_la_pregunta"]);
        Assert.Equal(1, motivos["sin_motivo"]);

        var causas = Conteos(json["fallosPorCausa"]!);
        Assert.Equal(1, causas["timeout_proveedor"]);
        Assert.Equal(1, causas["validador_rechazo"]);
        Assert.Equal(1, causas["sin_clasificar"]);
        Assert.Equal(1, causas["resultado_vacio"]);
        Assert.False(causas.ContainsKey("cupo_agotado"));

        var familiaDelVacio = json["fallosPorCausa"]!.AsArray()
            .Single(c => (string)c!["clave"]! == "resultado_vacio")!["familia"];
        Assert.Equal("senal", (string?)familiaDelVacio);

        // Policy denials: outside every rate, reported apart.
        var politica = Conteos(json["denegacionesDePolitica"]!);
        Assert.Equal(2, politica["cupo_agotado"]);

        var modelos = json["porModelo"]!.AsArray().ToDictionary(m => (string)m!["modelo"]!, m => m!["resumen"]!);
        Assert.Equal(8, (int)modelos[ModeloA]["turnos"]!);
        Assert.Equal(3, (int)modelos[ModeloA]["negativos"]!);
        Assert.Equal(3, (int)modelos[ModeloB]["turnos"]!);
        Assert.Equal(0, (int)modelos[ModeloB]["negativos"]!);
        Assert.Equal([ModeloB, ModeloA], json["modelosDisponibles"]!.AsArray().Select(m => (string)m!));
    }

    [Fact]
    public async Task Un_voto_cuenta_el_dia_de_su_turno_y_no_el_del_voto()
    {
        await SembrarAsync(TestContext.Current.CancellationToken);
        var ayer = await SembrarTurnoAsync(1, "Respondida", null, ModeloA);
        var hace3 = await SembrarTurnoAsync(3, "Respondida", null, ModeloA);

        // Both votes are cast today, well after their turns.
        await SembrarVotoAsync(ayer, false, ["otro"], diasDespues: 1);
        await SembrarVotoAsync(hace3, false, ["otro"], diasDespues: 3);

        var calidad = await ObtenerAsync($"{Ruta}?periodo=7");

        var serie = calidad.Serie.ToDictionary(p => p.Dia);
        Assert.Equal(1, serie[Hoy.AddDays(-1)].Negativos);
        Assert.Equal(1, serie[Hoy.AddDays(-3)].Negativos);
        Assert.False(serie.ContainsKey(Hoy));
    }

    [Fact]
    public async Task La_tendencia_se_omite_con_menos_de_20_votos_en_cualquiera_de_los_dos_periodos()
    {
        await SembrarAsync(TestContext.Current.CancellationToken);
        await SembrarVotosAsync(diasAtras: 2, positivos: 14, negativos: 10); // current: 24 votes
        await SembrarVotosAsync(diasAtras: 10, positivos: 6, negativos: 6); // previous: 12 votes

        var calidad = await ObtenerAsync($"{Ruta}?periodo=7");

        Assert.Equal(24, calidad.Resumen.Votos);
        Assert.NotNull(calidad.Resumen.TasaNegativa);
        Assert.Null(calidad.Tendencia);
        Assert.Equal("pocos_votos", calidad.MotivoSinTendencia);
    }

    [Fact]
    public async Task La_tendencia_compara_contra_el_periodo_anterior_de_igual_largo()
    {
        await SembrarAsync(TestContext.Current.CancellationToken);
        // Current 7 days: 40 votes, 20 negative. Previous 7 days (days 8 to 14): 25 votes,
        // 5 negative. Votes beyond them (day 15) must not be read as the previous period.
        await SembrarVotosAsync(diasAtras: 2, positivos: 20, negativos: 20);
        await SembrarVotosAsync(diasAtras: 9, positivos: 20, negativos: 5);
        await SembrarVotosAsync(diasAtras: 15, positivos: 0, negativos: 30);

        var calidad = await ObtenerAsync($"{Ruta}?periodo=7");

        Assert.NotNull(calidad.Tendencia);
        Assert.Null(calidad.MotivoSinTendencia);
        Assert.Equal(0.2, calidad.Tendencia.TasaAnterior, 6);
        Assert.Equal(0.3, calidad.Tendencia.Variacion, 6);
        Assert.Equal(25, calidad.Tendencia.VotosAnteriores);
        Assert.Equal(Hoy.AddDays(-6), calidad.Desde);
        Assert.Equal(Hoy, calidad.Hasta);
    }

    [Fact]
    public async Task Noventa_dias_no_tiene_tendencia_porque_el_periodo_anterior_queda_fuera_de_la_retencion()
    {
        await SembrarAsync(TestContext.Current.CancellationToken);
        await SembrarVotosAsync(diasAtras: 2, positivos: 30, negativos: 30);
        await SembrarVotosAsync(diasAtras: 20, positivos: 30, negativos: 30);

        var calidad = await ObtenerAsync($"{Ruta}?periodo=90");

        Assert.Null(calidad.Tendencia);
        Assert.Equal("fuera_de_retencion", calidad.MotivoSinTendencia);
        // The series of a 90-day period is weekly: every point is a Monday.
        Assert.True(calidad.PorSemana);
        Assert.All(calidad.Serie, p => Assert.Equal(DayOfWeek.Monday, p.Dia.DayOfWeek));
        Assert.Equal(120, calidad.Serie.Sum(p => p.Votos));
    }

    [Theory]
    [InlineData(45)]
    [InlineData(91)]
    [InlineData(0)]
    public async Task Un_periodo_fuera_de_7_30_o_90_se_rechaza(int periodo)
    {
        await SembrarAsync(TestContext.Current.CancellationToken);
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Sistemas, "sys_admin");

        var respuesta = await cliente.GetAsync($"{Ruta}?periodo={periodo}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Filtrar_por_modelo_cuenta_solo_sus_turnos_y_sus_votos()
    {
        await SembrarAsync(TestContext.Current.CancellationToken);
        await SembrarConjuntoAsync();

        var calidad = await ObtenerAsync($"{Ruta}?periodo=30&modelo={Uri.EscapeDataString(ModeloB)}");

        Assert.Equal(3, calidad.Resumen.Turnos);
        Assert.Equal(1, calidad.Resumen.Votos);
        Assert.Equal(0, calidad.Resumen.Negativos);
        var unico = Assert.Single(calidad.PorModelo);
        Assert.Equal(ModeloB, unico.Modelo);
        // The select still offers both models.
        Assert.Equal(2, calidad.ModelosDisponibles.Count);
    }

    [Fact]
    public async Task Sin_votos_las_tasas_son_nulas_y_no_cero()
    {
        await SembrarAsync(TestContext.Current.CancellationToken);
        await SembrarTurnoAsync(1, "Respondida", null, ModeloA);

        var json = await ObtenerJsonAsync($"{Ruta}?periodo=7");

        Assert.Null(json["resumen"]!["tasaNegativa"]);
        Assert.Equal(0d, (double)json["resumen"]!["cobertura"]!);
    }

    [Fact]
    public async Task Ni_los_agregados_ni_la_lista_devuelven_el_comentario_ni_al_actor_ni_la_hora_del_voto()
    {
        await SembrarAsync(TestContext.Current.CancellationToken);
        await SembrarConjuntoAsync();
        await ConcederRevisarCalidadAsync(Sistemas);

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Sistemas, "sys_admin");

        foreach (var url in new[]
        {
            $"{Ruta}?periodo=30",
            $"{Ruta}/problemas?periodo=30",
            $"{Ruta}/problemas?periodo=30&tipo=negativa",
        })
        {
            var cuerpo = await (await cliente.GetAsync(url, TestContext.Current.CancellationToken))
                .Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            Assert.DoesNotContain(ComentarioSecreto, cuerpo, StringComparison.Ordinal);
            Assert.DoesNotContain("comentario", cuerpo, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("actualizado", cuerpo, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("actor", cuerpo, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("lector", cuerpo, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Sistemas.ToString(), cuerpo, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Sin_asistente_administrar_los_agregados_responden_403()
    {
        await SembrarAsync(TestContext.Current.CancellationToken);
        await SembrarConjuntoAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var respuesta = await cliente.GetAsync($"{Ruta}?periodo=30", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.DoesNotContain("tasaNegativa", await respuesta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    // ------------------------------------------------------------- the problem list

    [Fact]
    public async Task Tener_solo_asistente_administrar_no_alcanza_para_la_lista_y_no_audita()
    {
        await SembrarAsync(TestContext.Current.CancellationToken);
        await SembrarConjuntoAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Sistemas, "sys_admin");

        var respuesta = await cliente.GetAsync($"{Ruta}/problemas?periodo=30", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal(0L, await EscalarAsync<long>("SELECT count(*) FROM asistente.auditoria_revision_de_calidad"));
    }

    [Fact]
    public async Task Tener_solo_asistente_revisar_calidad_lee_la_lista_pero_no_los_agregados()
    {
        await SembrarAsync(TestContext.Current.CancellationToken);
        await SembrarConjuntoAsync();
        await ConcederRevisarCalidadAsync(Secretaria);
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var lista = await cliente.GetAsync($"{Ruta}/problemas?periodo=30", TestContext.Current.CancellationToken);
        var agregados = await cliente.GetAsync($"{Ruta}?periodo=30", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, lista.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, agregados.StatusCode);
    }

    [Fact]
    public async Task La_lista_trae_cada_turno_una_vez_sin_politica_ni_vacios_y_audita_una_fila()
    {
        await SembrarAsync(TestContext.Current.CancellationToken);
        await SembrarConjuntoAsync();
        await ConcederRevisarCalidadAsync(Sistemas);
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Sistemas, "sys_admin");

        var pagina = await LeerAsync<PaginaDeProblemas>(
            await cliente.GetAsync($"{Ruta}/problemas?periodo=30", TestContext.Current.CancellationToken));

        // The 7 distinct turns with problems: no quota refusal, no empty result, and the turn
        // that is negative and replaced is one row.
        Assert.Equal(7, pagina.Total);
        Assert.Equal(7, pagina.Filas.Count);
        Assert.DoesNotContain(pagina.Filas, f => f.Pregunta.StartsWith("politica", StringComparison.Ordinal));
        Assert.DoesNotContain(pagina.Filas, f => f.Pregunta.StartsWith("vacio", StringComparison.Ordinal));
        Assert.Equal(1, pagina.Filas.Count(f => f.Pregunta == "negativa y reemplazada"));

        var conMotivos = pagina.Filas.Single(f => f.Pregunta == "dos motivos");
        Assert.Equal("negativa", conMotivos.Tipo);
        Assert.Equal("datos_incorrectos,faltan_datos", conMotivos.Motivo);
        Assert.Equal(ModeloA, conMotivos.Modelo);
        Assert.Equal(Hoy.AddDays(-1), conMotivos.Dia);
        Assert.Equal("Respondida", conMotivos.Estado);

        var fallo = pagina.Filas.Single(f => f.Pregunta == "tiempo agotado");
        Assert.Equal("fallo", fallo.Tipo);
        Assert.Equal("timeout_proveedor", fallo.Motivo);

        var sinClasificar = pagina.Filas.Single(f => f.Pregunta == "sin causa");
        Assert.Equal("sin_clasificar", sinClasificar.Motivo);

        // One audit row, with who read, when, and what was asked and returned.
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            "SELECT lector_id, ocurrido_en, filtros, filas FROM asistente.auditoria_revision_de_calidad", conexion);
        await using var lector = await comando.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await lector.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(Sistemas, lector.GetGuid(0));
        Assert.True(lector.GetFieldValue<DateTimeOffset>(1) > DateTimeOffset.UtcNow.AddMinutes(-5));
        Assert.Equal("periodo=30;modelo=todos;tipo=todos;pagina=1", lector.GetString(2));
        Assert.Equal(7, lector.GetInt32(3));
        Assert.False(await lector.ReadAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("negativa", 3)]
    [InlineData("fallo", 3)]
    [InlineData("reemplazo", 2)]
    public async Task El_filtro_por_tipo_trae_solo_ese_tipo(string tipo, int esperadas)
    {
        await SembrarAsync(TestContext.Current.CancellationToken);
        await SembrarConjuntoAsync();
        await ConcederRevisarCalidadAsync(Sistemas);
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Sistemas, "sys_admin");

        var pagina = await LeerAsync<PaginaDeProblemas>(await cliente.GetAsync(
            $"{Ruta}/problemas?periodo=30&tipo={tipo}", TestContext.Current.CancellationToken));

        Assert.Equal(esperadas, pagina.Total);
        Assert.All(pagina.Filas, f => Assert.Equal(tipo, f.Tipo));
    }

    [Fact]
    public async Task La_lista_se_pagina_de_a_25_y_cada_pagina_se_audita()
    {
        await SembrarAsync(TestContext.Current.CancellationToken);
        await ConcederRevisarCalidadAsync(Sistemas);
        for (var i = 0; i < 30; i++)
        {
            var turno = await SembrarTurnoAsync(i % 5, "Respondida", null, ModeloA, pregunta: $"pregunta {i:00}");
            await SembrarVotoAsync(turno, false, ["otro"]);
        }

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Sistemas, "sys_admin");
        var ct = TestContext.Current.CancellationToken;

        var primera = await LeerAsync<PaginaDeProblemas>(
            await cliente.GetAsync($"{Ruta}/problemas?periodo=7&pagina=1", ct));
        var segunda = await LeerAsync<PaginaDeProblemas>(
            await cliente.GetAsync($"{Ruta}/problemas?periodo=7&pagina=2", ct));

        Assert.Equal(30, primera.Total);
        Assert.Equal(25, primera.Filas.Count);
        Assert.Equal(5, segunda.Filas.Count);
        // No row is on both pages.
        Assert.Empty(primera.Filas.Select(f => f.Pregunta).Intersect(segunda.Filas.Select(f => f.Pregunta)));
        Assert.Equal(2L, await EscalarAsync<long>("SELECT count(*) FROM asistente.auditoria_revision_de_calidad"));
        Assert.Equal(5, await EscalarAsync<int>(
            "SELECT filas FROM asistente.auditoria_revision_de_calidad WHERE filtros LIKE '%pagina=2'"));
    }

    [Theory]
    [InlineData("periodo=45")]
    [InlineData("tipo=inventado")]
    [InlineData("pagina=0")]
    [InlineData("pagina=-3")]
    public async Task Un_pedido_invalido_de_la_lista_se_rechaza_y_no_audita(string consulta)
    {
        await SembrarAsync(TestContext.Current.CancellationToken);
        await ConcederRevisarCalidadAsync(Sistemas);
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Sistemas, "sys_admin");

        var respuesta = await cliente.GetAsync($"{Ruta}/problemas?{consulta}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(0L, await EscalarAsync<long>("SELECT count(*) FROM asistente.auditoria_revision_de_calidad"));
    }

    // ------------------------------------------------------------------ seeding

    /// <summary>
    /// The set the aggregate assertions are computed from. Model A has eight attended turns and
    /// model B three; two quota refusals are policy. The vote of each is cast days after its turn.
    /// </summary>
    private async Task SembrarConjuntoAsync()
    {
        var dosMotivos = await SembrarTurnoAsync(1, "Respondida", null, ModeloA, pregunta: "dos motivos");
        await SembrarVotoAsync(dosMotivos, false, ["datos_incorrectos", "faltan_datos"], ComentarioSecreto, 2);

        var sinMotivo = await SembrarTurnoAsync(2, "Respondida", null, ModeloA, pregunta: "sin motivo");
        await SembrarVotoAsync(sinMotivo, false, null, null, 1);

        var positivo = await SembrarTurnoAsync(2, "Respondida", null, ModeloA, pregunta: "positiva");
        await SembrarVotoAsync(positivo, true, null, null, 1);

        await SembrarTurnoAsync(3, "Respondida", null, ModeloA, reemplazado: true, pregunta: "reemplazada");

        var ambas = await SembrarTurnoAsync(1, "Respondida", null, ModeloA, reemplazado: true, pregunta: "negativa y reemplazada");
        await SembrarVotoAsync(ambas, false, ["otro"], null, 1);

        await SembrarTurnoAsync(4, "ServicioDegradado", "timeout_proveedor", ModeloA, pregunta: "tiempo agotado");
        await SembrarTurnoAsync(4, "Respondida", "resultado_vacio", ModeloA, pregunta: "vacio");
        await SembrarTurnoAsync(5, "ServicioDegradado", null, ModeloA, pregunta: "sin causa");

        await SembrarTurnoAsync(1, "ServicioDegradado", "cupo_agotado", ModeloA, pregunta: "politica 1");
        await SembrarTurnoAsync(1, "ServicioDegradado", "cupo_agotado", ModeloA, pregunta: "politica 2");

        var bPositivo = await SembrarTurnoAsync(1, "Respondida", null, ModeloB, pregunta: "b positiva");
        await SembrarVotoAsync(bPositivo, true, null, null, 2);
        await SembrarTurnoAsync(3, "NoContestable", "validador_rechazo", ModeloB, pregunta: "validador");
        await SembrarTurnoAsync(3, "Respondida", null, ModeloB, pregunta: "b sin voto");
    }

    private async Task SembrarVotosAsync(int diasAtras, int positivos, int negativos)
    {
        for (var i = 0; i < positivos + negativos; i++)
        {
            var turno = await SembrarTurnoAsync(diasAtras, "Respondida", null, ModeloA);
            await SembrarVotoAsync(turno, i < positivos, i < positivos ? null : ["otro"]);
        }
    }

    private async Task<Guid> SembrarTurnoAsync(
        int diasAtras, string estado, string? causa, string? modelo,
        bool reemplazado = false, string pregunta = "pregunta")
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = Preparar(
            conexion,
            """
            INSERT INTO asistente.registro_analitico (pregunta, categoria, estado, dia, causa, modelo, reemplazado)
            VALUES (@pregunta, 'consulta', @estado, @dia, @causa, @modelo, @reemplazado)
            RETURNING id
            """,
            ("pregunta", pregunta),
            ("estado", estado),
            ("dia", Hoy.AddDays(-diasAtras)),
            ("causa", (object?)causa ?? DBNull.Value),
            ("modelo", (object?)modelo ?? DBNull.Value),
            ("reemplazado", reemplazado));

        return (Guid)(await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private async Task SembrarVotoAsync(
        Guid turno, bool positivo, string[]? razones = null, string? comentario = null, int diasDespues = 0)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = Preparar(
            conexion,
            """
            INSERT INTO asistente.retroalimentacion_turno (analitico_id, voto, razones, comentario, actualizado_en)
            SELECT id, @voto, @razones, @comentario, dia::timestamptz + make_interval(days => @despues)
              FROM asistente.registro_analitico WHERE id = @turno
            """,
            ("turno", turno),
            ("voto", positivo),
            ("razones", (object?)razones ?? DBNull.Value),
            ("comentario", (object?)comentario ?? DBNull.Value),
            ("despues", diasDespues));
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task ConcederRevisarCalidadAsync(Guid actor)
    {
        // No role has this permission by default: grant it through the user's roles, as
        // /membresia-roles would.
        await EjecutarAsync(
            """
            INSERT INTO identity.rol_permisos (rol_id, permiso_id)
            SELECT ur.role_id, p.id
              FROM identity.user_roles ur
              JOIN identity.permisos p ON p.code = 'asistente.revisar_calidad'
             WHERE ur.user_id = @actor
            ON CONFLICT (rol_id, permiso_id) DO NOTHING
            """,
            ("actor", actor));
    }

    // ------------------------------------------------------------------ support

    private async Task<JsonNode> ObtenerJsonAsync(string url)
    {
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Sistemas, "sys_admin");

        var respuesta = await cliente.GetAsync(url, TestContext.Current.CancellationToken);
        var cuerpo = await respuesta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(respuesta.IsSuccessStatusCode, cuerpo);

        return JsonNode.Parse(cuerpo)!;
    }

    private async Task<CalidadAgregada> ObtenerAsync(string url)
    {
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Sistemas, "sys_admin");

        return await LeerAsync<CalidadAgregada>(await cliente.GetAsync(url, TestContext.Current.CancellationToken));
    }

    private static Dictionary<string, int> Conteos(JsonNode lista) =>
        lista.AsArray().ToDictionary(c => (string)c!["clave"]!, c => (int)c!["cantidad"]!);

    private static void Autenticar(HttpClient cliente, Guid usuario, string rol)
    {
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, usuario.ToString());
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, rol);
    }

    private static async Task<T> LeerAsync<T>(HttpResponseMessage respuesta)
    {
        var ct = TestContext.Current.CancellationToken;
        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);

        Assert.True(respuesta.IsSuccessStatusCode, cuerpo);

        return (await respuesta.Content.ReadFromJsonAsync<T>(ct))!;
    }

    private WebApplicationFactory<Program> CrearHost() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(servicios => servicios.AddSingleton<TimeProvider>(new RelojFijo(Ahora)));
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
        });
}
