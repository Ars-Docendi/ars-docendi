using System.Net;
using System.Net.Http.Json;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Modules.Asistente;
using Modules.Asistente.Api;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// El historial propio, por HTTP de verdad (asistente-historial-conversaciones §6-8).
/// </summary>
public sealed class HistorialControllerTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_historial_endpoint")
{
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    private static readonly Guid Coordinador = Guid.Parse("a0000000-0000-4000-8000-000000000003");
    private static readonly DateTimeOffset Ancla = new(2027, 5, 1, 10, 0, 0, TimeSpan.Zero);

    // ------------------------------------------------------------------ listar

    [Fact]
    public async Task Lista_solo_las_conversaciones_propias()
    {
        await SembrarAsync();

        var propia = await SembrarHiloAsync(Secretaria, "propia", Ancla);
        await SembrarTurnoAsync(propia, "¿cuántos docentes hay?", "SELECT 1");
        var ajena = await SembrarHiloAsync(Coordinador, "ajena", Ancla);
        await SembrarTurnoAsync(ajena, "otra pregunta", "SELECT 2");

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var lista = await LeerAsync<List<ConversacionResumenDto>>(
            await cliente.GetAsync("/api/asistente/historial", TestContext.Current.CancellationToken));

        Assert.Single(lista);
        Assert.Equal(propia, lista[0].Id);
    }

    [Fact]
    public async Task La_busqueda_encuentra_una_conversacion_por_su_pregunta()
    {
        await SembrarAsync();

        var conDesignaciones = await SembrarHiloAsync(Secretaria, "sobre designaciones", Ancla);
        await SembrarTurnoAsync(
            conDesignaciones, "¿cuántas designaciones vencen este cuatrimestre?", "SELECT 1");

        var conAulas = await SembrarHiloAsync(Secretaria, "sobre aulas", Ancla.AddMinutes(1));
        await SembrarTurnoAsync(conAulas, "¿qué aulas están libres?", "SELECT 2");

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var lista = await LeerAsync<List<ConversacionResumenDto>>(await cliente.GetAsync(
            "/api/asistente/historial?q=designaciones", TestContext.Current.CancellationToken));

        Assert.Single(lista);
        Assert.Equal(conDesignaciones, lista[0].Id);
    }

    // ------------------------------------------------------------------ obtener

    [Fact]
    public async Task Obtener_una_conversacion_ajena_da_404()
    {
        await SembrarAsync();

        var ajena = await SembrarHiloAsync(Coordinador, "ajena", Ancla);
        await SembrarTurnoAsync(ajena, "algo", "SELECT 1");

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var respuesta = await cliente.GetAsync(
            $"/api/asistente/historial/{ajena}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Sin_el_permiso_de_ver_la_consulta_el_campo_sql_no_viaja()
    {
        await SembrarAsync();

        var propia = await SembrarHiloAsync(Secretaria, "propia", Ancla);
        await SembrarTurnoAsync(propia, "¿cuántos docentes hay?", "SELECT count(*) FROM x");

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        // Secretaría no tiene asistente.ver_consulta en el seed de roles.
        Autenticar(cliente, Secretaria, "secretaria");

        var detalle = await LeerAsync<ConversacionDetalleDto>(await cliente.GetAsync(
            $"/api/asistente/historial/{propia}", TestContext.Current.CancellationToken));

        Assert.Single(detalle.Turnos);
        Assert.Null(detalle.Turnos[0].Sql);
        Assert.Equal("¿cuántos docentes hay?", detalle.Turnos[0].Pregunta);
    }

    [Fact]
    public async Task Con_el_permiso_de_ver_la_consulta_el_campo_sql_si_viaja()
    {
        await SembrarAsync();
        await ConcederVerConsultaASecretariaAsync();

        var propia = await SembrarHiloAsync(Secretaria, "propia", Ancla);
        await SembrarTurnoAsync(propia, "¿cuántos docentes hay?", "SELECT count(*) FROM x");

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var detalle = await LeerAsync<ConversacionDetalleDto>(await cliente.GetAsync(
            $"/api/asistente/historial/{propia}", TestContext.Current.CancellationToken));

        Assert.Equal("SELECT count(*) FROM x", detalle.Turnos[0].Sql);
    }

    // ------------------------------------------------------------------ renombrar

    [Fact]
    public async Task Renombrar_una_conversacion_propia_cambia_el_titulo()
    {
        await SembrarAsync();

        var propia = await SembrarHiloAsync(Secretaria, "viejo título", Ancla);

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var respuesta = await cliente.PatchAsJsonAsync(
            $"/api/asistente/historial/{propia}",
            new RenombrarConversacionDto("nuevo título"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.Equal(
            "nuevo título",
            await EscalarAsync<string>("SELECT titulo FROM asistente.hilo_historico WHERE id = @id", ("id", propia)));
    }

    [Fact]
    public async Task Renombrar_una_conversacion_ajena_se_rechaza()
    {
        await SembrarAsync();

        var ajena = await SembrarHiloAsync(Coordinador, "ajena", Ancla);

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var respuesta = await cliente.PatchAsJsonAsync(
            $"/api/asistente/historial/{ajena}",
            new RenombrarConversacionDto("robado"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal(
            "ajena",
            await EscalarAsync<string>("SELECT titulo FROM asistente.hilo_historico WHERE id = @id", ("id", ajena)));
    }

    // ------------------------------------------------------------------ archivar

    [Fact]
    public async Task Archivar_una_conversacion_propia_la_marca()
    {
        await SembrarAsync();
        var propia = await SembrarHiloAsync(Secretaria, "propia", Ancla);

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var respuesta = await cliente.PostAsync(
            $"/api/asistente/historial/{propia}/archivar", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        var lista = await LeerAsync<List<ConversacionResumenDto>>(
            await cliente.GetAsync("/api/asistente/historial", TestContext.Current.CancellationToken));
        Assert.True(Assert.Single(lista).Archivada);
    }

    [Fact]
    public async Task Desarchivar_le_quita_la_marca()
    {
        await SembrarAsync();
        var propia = await SembrarHiloAsync(Secretaria, "propia", Ancla);

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        await cliente.PostAsync(
            $"/api/asistente/historial/{propia}/archivar", null, TestContext.Current.CancellationToken);
        var respuesta = await cliente.PostAsync(
            $"/api/asistente/historial/{propia}/desarchivar", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        var lista = await LeerAsync<List<ConversacionResumenDto>>(
            await cliente.GetAsync("/api/asistente/historial", TestContext.Current.CancellationToken));
        Assert.False(Assert.Single(lista).Archivada);
    }

    [Theory]
    [InlineData("archivar")]
    [InlineData("desarchivar")]
    public async Task Archivar_o_desarchivar_una_conversacion_ajena_da_404(string accion)
    {
        await SembrarAsync();
        var ajena = await SembrarHiloAsync(Coordinador, "ajena", Ancla);

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var respuestaAjena = await cliente.PostAsync(
            $"/api/asistente/historial/{ajena}/{accion}", null, TestContext.Current.CancellationToken);
        var respuestaInexistente = await cliente.PostAsync(
            $"/api/asistente/historial/{Guid.NewGuid()}/{accion}", null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, respuestaAjena.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, respuestaInexistente.StatusCode);
        Assert.Equal(
            await respuestaAjena.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            await respuestaInexistente.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task La_busqueda_encuentra_una_conversacion_archivada_y_la_marca()
    {
        await SembrarAsync();
        var archivada = await SembrarHiloAsync(Secretaria, "sobre designaciones", Ancla);
        await SembrarTurnoAsync(archivada, "¿cuántas designaciones vencen este cuatrimestre?", "SELECT 1");

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        await cliente.PostAsync(
            $"/api/asistente/historial/{archivada}/archivar", null, TestContext.Current.CancellationToken);

        var lista = await LeerAsync<List<ConversacionResumenDto>>(await cliente.GetAsync(
            "/api/asistente/historial?q=designaciones", TestContext.Current.CancellationToken));

        Assert.True(Assert.Single(lista).Archivada);
    }

    [Fact]
    public async Task Archivar_no_toca_la_ultima_actividad()
    {
        await SembrarAsync();
        var vieja = Ancla.AddDays(-100);
        var propia = await SembrarHiloAsync(Secretaria, "propia", vieja);

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        await cliente.PostAsync(
            $"/api/asistente/historial/{propia}/archivar", null, TestContext.Current.CancellationToken);

        Assert.Equal(vieja.UtcDateTime, await EscalarAsync<DateTime>(
            "SELECT ultima_actividad FROM asistente.hilo_historico WHERE id = @id", ("id", propia)));
    }

    // ------------------------------------------------------------------ eliminar

    [Fact]
    public async Task Eliminar_devuelve_el_lote_y_la_conversacion_desaparece_de_la_lista_de_inmediato()
    {
        await SembrarAsync();

        var unaConversacion = await SembrarHiloAsync(Secretaria, "uno", Ancla);
        var otraConversacion = await SembrarHiloAsync(Secretaria, "dos", Ancla);

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var lote = await LeerAsync<LoteDeBorradoDto>(await cliente.DeleteAsync(
            $"/api/asistente/historial/{unaConversacion}", TestContext.Current.CancellationToken));

        Assert.NotEqual(Guid.Empty, lote.LoteDeBorrado);

        var lista = await LeerAsync<List<ConversacionResumenDto>>(
            await cliente.GetAsync("/api/asistente/historial", TestContext.Current.CancellationToken));
        Assert.Single(lista);
        Assert.Equal(otraConversacion, lista[0].Id);

        // TODAVÍA EXISTE EN LA BASE — el DELETE marca, no borra. Ver
        // «Un_borrado_fuera_de_ventana_se_purga_y_ya_no_existe» para la
        // finalidad física.
        Assert.Equal(1L, await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.hilo_historico WHERE id = @id", ("id", unaConversacion)));
    }

    [Fact]
    public async Task Una_conversacion_pendiente_es_invisible_para_obtener_reanudar_y_renombrar()
    {
        await SembrarAsync();
        var propia = await SembrarHiloAsync(Secretaria, "propia", Ancla);

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        await cliente.DeleteAsync(
            $"/api/asistente/historial/{propia}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync(
            $"/api/asistente/historial/{propia}", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.PostAsync(
            $"/api/asistente/historial/{propia}/reanudar", null,
            TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.PatchAsJsonAsync(
            $"/api/asistente/historial/{propia}",
            new RenombrarConversacionDto("robado"),
            TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Eliminar_una_conversacion_ajena_se_rechaza_y_sigue_existiendo()
    {
        await SembrarAsync();

        var ajena = await SembrarHiloAsync(Coordinador, "ajena", Ancla);

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var respuesta = await cliente.DeleteAsync(
            $"/api/asistente/historial/{ajena}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal(1L, await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.hilo_historico WHERE id = @id AND borrado_pendiente_desde IS NULL",
            ("id", ajena)));
    }

    [Fact]
    public async Task Eliminar_todo_marca_las_propias_archivadas_incluidas_y_deja_la_ajena()
    {
        await SembrarAsync();

        var unaPropia = await SembrarHiloAsync(Secretaria, "uno", Ancla);
        var archivada = await SembrarHiloAsync(Secretaria, "dos", Ancla);
        var ajena = await SembrarHiloAsync(Coordinador, "ajena", Ancla);

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");
        await cliente.PostAsync(
            $"/api/asistente/historial/{archivada}/archivar", null, TestContext.Current.CancellationToken);

        var lote = await LeerAsync<LoteDeBorradoDto>(await cliente.DeleteAsync(
            "/api/asistente/historial", TestContext.Current.CancellationToken));

        Assert.NotEqual(Guid.Empty, lote.LoteDeBorrado);
        Assert.Empty(await LeerAsync<List<ConversacionResumenDto>>(
            await cliente.GetAsync("/api/asistente/historial", TestContext.Current.CancellationToken)));
        Assert.Equal(2L, await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.hilo_historico WHERE lote_de_borrado = @lote",
            ("lote", lote.LoteDeBorrado)));
        Assert.Equal(1L, await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.hilo_historico WHERE id = @id AND borrado_pendiente_desde IS NULL",
            ("id", ajena)));

        _ = unaPropia;
    }

    // -------------------------------------------------------------- deshacer

    [Fact]
    public async Task Deshacer_dentro_de_la_ventana_restaura_la_conversacion_sin_cambios()
    {
        await SembrarAsync();
        var propia = await SembrarHiloAsync(Secretaria, "propia", Ancla);
        await SembrarTurnoAsync(propia, "¿cuántos hay?", "SELECT 1");

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var lote = await LeerAsync<LoteDeBorradoDto>(await cliente.DeleteAsync(
            $"/api/asistente/historial/{propia}", TestContext.Current.CancellationToken));

        var respuesta = await cliente.PostAsync(
            $"/api/asistente/historial/borrados/{lote.LoteDeBorrado}/deshacer", null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        var lista = await LeerAsync<List<ConversacionResumenDto>>(
            await cliente.GetAsync("/api/asistente/historial", TestContext.Current.CancellationToken));
        var restaurada = Assert.Single(lista);
        Assert.Equal(propia, restaurada.Id);
        Assert.Equal("propia", restaurada.Titulo);
    }

    [Fact]
    public async Task Deshacer_un_lote_ajeno_da_404_y_no_restaura_nada()
    {
        await SembrarAsync();
        var ajena = await SembrarHiloAsync(Coordinador, "ajena", Ancla);
        var lote = Guid.NewGuid();
        await MarcarPendienteAsync(ajena, Ancla, lote);

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var respuesta = await cliente.PostAsync(
            $"/api/asistente/historial/borrados/{lote}/deshacer", null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal(1L, await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.hilo_historico WHERE id = @id AND borrado_pendiente_desde IS NOT NULL",
            ("id", ajena)));
    }

    [Fact]
    public async Task Deshacer_un_lote_desconocido_da_404()
    {
        await SembrarAsync();

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var respuesta = await cliente.PostAsync(
            $"/api/asistente/historial/borrados/{Guid.NewGuid()}/deshacer", null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Deshacer_fuera_de_la_ventana_da_404_y_la_conversacion_sigue_pendiente()
    {
        await SembrarAsync();
        var propia = await SembrarHiloAsync(Secretaria, "propia", Ancla);

        var reloj = new RelojFijo(Ancla);
        using var host = CrearHost(reloj: reloj);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var lote = await LeerAsync<LoteDeBorradoDto>(await cliente.DeleteAsync(
            $"/api/asistente/historial/{propia}", TestContext.Current.CancellationToken));

        reloj.Avanzar(TimeSpan.FromSeconds(16));

        var respuesta = await cliente.PostAsync(
            $"/api/asistente/historial/borrados/{lote.LoteDeBorrado}/deshacer", null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal(1L, await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.hilo_historico WHERE id = @id AND borrado_pendiente_desde IS NOT NULL",
            ("id", propia)));
    }

    [Fact]
    public async Task Borrar_una_y_despues_borrar_todo_deshacer_todo_deja_la_primera_pendiente_por_su_cuenta()
    {
        // EL CASO EXACTO DE LA SPEC: A se borra sola; dentro de su ventana se
        // borran todas. Deshacer el lote de «todas» restaura únicamente ese
        // lote — A sigue pendiente, en el suyo.
        await SembrarAsync();
        var a = await SembrarHiloAsync(Secretaria, "a", Ancla);
        var b = await SembrarHiloAsync(Secretaria, "b", Ancla);

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var loteDeA = await LeerAsync<LoteDeBorradoDto>(await cliente.DeleteAsync(
            $"/api/asistente/historial/{a}", TestContext.Current.CancellationToken));
        var loteDeTodas = await LeerAsync<LoteDeBorradoDto>(await cliente.DeleteAsync(
            "/api/asistente/historial", TestContext.Current.CancellationToken));

        Assert.NotEqual(loteDeA.LoteDeBorrado, loteDeTodas.LoteDeBorrado);

        var respuesta = await cliente.PostAsync(
            $"/api/asistente/historial/borrados/{loteDeTodas.LoteDeBorrado}/deshacer", null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        var lista = await LeerAsync<List<ConversacionResumenDto>>(
            await cliente.GetAsync("/api/asistente/historial", TestContext.Current.CancellationToken));
        Assert.Equal(b, Assert.Single(lista).Id);
        Assert.Equal(1L, await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.hilo_historico WHERE id = @id AND lote_de_borrado = @lote",
            ("id", a), ("lote", loteDeA.LoteDeBorrado)));
    }

    // -------------------------------------------------------------- el barrido

    [Fact]
    public async Task Un_borrado_fuera_de_la_ventana_se_purga_por_el_barrido_y_desaparece_de_verdad()
    {
        await SembrarAsync();
        var propia = await SembrarHiloAsync(Secretaria, "propia", Ancla);
        await SembrarTurnoAsync(propia, "¿cuántos hay?", "SELECT 1");

        var reloj = new RelojFijo(Ancla);
        using var host = CrearHost(reloj: reloj);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        await cliente.DeleteAsync(
            $"/api/asistente/historial/{propia}", TestContext.Current.CancellationToken);
        reloj.Avanzar(TimeSpan.FromSeconds(16));

        var barrido = new BarridoDePendientes(
            new CadenaDuena(Cadena), Options.Create(new OpcionesAsistente()), reloj);
        var borradas = await barrido.BarrerAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, borradas);
        Assert.Equal(0L, await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.hilo_historico WHERE id = @id", ("id", propia)));
        Assert.Equal(0L, await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.turno_historico WHERE hilo_id = @id", ("id", propia)));
    }

    // ------------------------------------------------------------------ reanudar

    [Fact]
    public async Task Reanudar_devuelve_un_hilo_nuevo_con_los_turnos_pasados()
    {
        await SembrarAsync();

        var propia = await SembrarHiloAsync(Secretaria, "una charla", Ancla);
        await SembrarTurnoAsync(propia, "¿y el de Gómez?", "SELECT 1", Ancla);
        await SembrarTurnoAsync(propia, "¿y el de Pérez?", "SELECT 2", Ancla.AddMinutes(2));

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var reanudada = await LeerAsync<ReanudarDto>(await cliente.PostAsync(
            $"/api/asistente/historial/{propia}/reanudar", null,
            TestContext.Current.CancellationToken));

        Assert.NotEqual(Guid.Empty, reanudada.Hilo);
        Assert.Equal(2, reanudada.Turnos.Count);
    }

    [Fact]
    public async Task Reanudar_una_conversacion_ajena_se_rechaza()
    {
        await SembrarAsync();

        var ajena = await SembrarHiloAsync(Coordinador, "ajena", Ancla);

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var respuesta = await cliente.PostAsync(
            $"/api/asistente/historial/{ajena}/reanudar", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    // ------------------------------------------------------------------ reejecutar

    [Fact]
    public async Task Reejecutar_un_turno_respondido_devuelve_una_tabla_de_verdad_sin_llamar_al_modelo()
    {
        await SembrarAsync();

        var propia = await SembrarHiloAsync(Secretaria, "una charla", Ancla);
        var turno = await SembrarTurnoAsync(
            propia, "¿cuántas designaciones hay?", "SELECT count(*) AS total FROM designaciones.designaciones");

        // GUION VACÍO A PROPÓSITO: si algún camino llegara a llamar al modelo,
        // el proveedor guionado revienta por falta de respuestas en vez de
        // servir una cualquiera — es la prueba dura de «cero llamadas», no
        // sólo una inspección de código.
        var guionado = new ProveedorGuionado();
        using var host = CrearHost(guionado);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var reejecucion = await LeerAsync<ReejecucionDto>(await cliente.PostAsync(
            $"/api/asistente/historial/turnos/{turno}/reejecutar", null,
            TestContext.Current.CancellationToken));

        Assert.True(reejecucion.Exitosa);
        Assert.Single(reejecucion.Columnas);
        Assert.Single(reejecucion.Filas);
        Assert.Equal(0, guionado.Llamadas);

        // NO escribió una fila nueva de historial, ni de ningún registro: no
        // es un turno, y contarlo como uno inflaría las métricas de uso con
        // una acción que no gastó ni modelo ni cupo (design.md D4).
        Assert.Equal(1L, await EscalarAsync<long>("SELECT count(*) FROM asistente.hilo_historico"));
        Assert.Equal(1L, await EscalarAsync<long>("SELECT count(*) FROM asistente.turno_historico"));
        Assert.Equal(0L, await EscalarAsync<long>("SELECT count(*) FROM asistente.registro_operativo"));
        Assert.Equal(0L, await EscalarAsync<long>("SELECT count(*) FROM asistente.registro_analitico"));
    }

    [Fact]
    public async Task Reejecutar_un_turno_sin_respuesta_se_rechaza()
    {
        await SembrarAsync();

        var propia = await SembrarHiloAsync(Secretaria, "una charla", Ancla);
        var turno = await SembrarTurnoAsync(
            propia, "no sé qué preguntar", null, Ancla, estado: "NoContestable");

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var respuesta = await cliente.PostAsync(
            $"/api/asistente/historial/turnos/{turno}/reejecutar", null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Reejecutar_un_turno_ajeno_se_rechaza()
    {
        await SembrarAsync();

        var ajena = await SembrarHiloAsync(Coordinador, "ajena", Ancla);
        var turno = await SembrarTurnoAsync(ajena, "algo", "SELECT 1");

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var respuesta = await cliente.PostAsync(
            $"/api/asistente/historial/turnos/{turno}/reejecutar", null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Reejecutar_una_sql_que_ya_no_corre_falla_amigablemente()
    {
        await SembrarAsync();

        var propia = await SembrarHiloAsync(Secretaria, "una charla", Ancla);
        var turno = await SembrarTurnoAsync(
            propia, "¿cuántos hay?", "SELECT * FROM una_tabla_que_no_existe_mas");

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var reejecucion = await LeerAsync<ReejecucionDto>(await cliente.PostAsync(
            $"/api/asistente/historial/turnos/{turno}/reejecutar", null,
            TestContext.Current.CancellationToken));

        Assert.False(reejecucion.Exitosa);
        Assert.NotNull(reejecucion.Mensaje);
        Assert.DoesNotContain("42P01", reejecucion.Mensaje, StringComparison.Ordinal);
        Assert.Empty(reejecucion.Filas);
    }

    // ------------------------------------------------------------------ apoyo

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

    private WebApplicationFactory<Program> CrearHost(
        ProveedorGuionado? guionado = null, TimeProvider? reloj = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            if (guionado is not null)
            {
                builder.ConfigureTestServices(servicios =>
                    servicios.AddSingleton(new ProveedorBase(guionado)));
            }

            // Reemplaza al `TimeProvider.System` que el módulo registra con
            // `TryAddSingleton` — la última registración gana, mismo truco
            // que el `ProveedorBase` de arriba — para poder adelantar la
            // ventana de «Deshacer» sin esperarla de verdad.
            if (reloj is not null)
            {
                builder.ConfigureTestServices(servicios => servicios.AddSingleton(reloj));
            }

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
        });

    private async Task ConcederVerConsultaASecretariaAsync()
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO identity.rol_permisos (rol_id, permiso_id)
            SELECT r.id, p.id FROM identity.roles r
            CROSS JOIN identity.permisos p
            WHERE r.code = 'secretaria' AND p.code = 'asistente.ver_consulta'
            ON CONFLICT (rol_id, permiso_id) DO NOTHING
            """, conexion);

        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Guid> SembrarHiloAsync(Guid actor, string titulo, DateTimeOffset actividad)
    {
        var id = Guid.NewGuid();
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO asistente.hilo_historico (id, actor_id, titulo, creado_en, ultima_actividad)
            VALUES (@id, @actor, @titulo, @actividad, @actividad)
            """, conexion);

        comando.Parameters.AddWithValue("id", id);
        comando.Parameters.AddWithValue("actor", actor);
        comando.Parameters.AddWithValue("titulo", titulo);
        comando.Parameters.AddWithValue("actividad", actividad);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        return id;
    }

    /// <summary>Marca un hilo ya sembrado como pendiente de borrado, sin pasar por el endpoint.</summary>
    private async Task MarcarPendienteAsync(Guid hiloId, DateTimeOffset desde, Guid lote)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            UPDATE asistente.hilo_historico
               SET borrado_pendiente_desde = @desde, lote_de_borrado = @lote
             WHERE id = @hilo
            """, conexion);

        comando.Parameters.AddWithValue("desde", desde);
        comando.Parameters.AddWithValue("lote", lote);
        comando.Parameters.AddWithValue("hilo", hiloId);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Guid> SembrarTurnoAsync(
        Guid hiloId, string pregunta, string? sql, DateTimeOffset? ocurrioEn = null,
        string estado = "Respondida")
    {
        var id = Guid.NewGuid();
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO asistente.turno_historico (id, hilo_id, pregunta, sql_resuelto, estado, ocurrido_en)
            VALUES (@id, @hilo, @pregunta, @sql, @estado, @ahora)
            """, conexion);

        comando.Parameters.AddWithValue("id", id);
        comando.Parameters.AddWithValue("hilo", hiloId);
        comando.Parameters.AddWithValue("pregunta", pregunta);
        comando.Parameters.AddWithValue(
            "sql", NpgsqlTypes.NpgsqlDbType.Text, (object?)sql ?? DBNull.Value);
        comando.Parameters.AddWithValue("estado", estado);
        comando.Parameters.AddWithValue("ahora", ocurrioEn ?? Ancla);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        return id;
    }
}
