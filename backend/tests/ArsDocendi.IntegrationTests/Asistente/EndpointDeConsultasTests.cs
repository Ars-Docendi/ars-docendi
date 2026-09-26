using System.Net;
using System.Net.Http.Json;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Modules.Asistente;
using Modules.Asistente.Api;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica la puerta del turno: permiso, idempotencia y forma de la respuesta.
/// </summary>
/// <remarks>
/// Va por HTTP de verdad y no llamando al servicio: lo que se prueba acá es
/// exactamente lo que el borde agrega —autorización, cabeceras, códigos de estado y
/// la traducción al contrato— y nada de eso se ejercita invocando la capa.
/// </remarks>
public sealed class EndpointDeConsultasTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_endpoint")
{
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    private static readonly Guid Coordinador = Guid.Parse("a0000000-0000-4000-8000-000000000003");
    private static readonly Guid Docente = Guid.Parse("a0000000-0000-4000-8000-000000000001");

    private const string Ruta = "/api/asistente/consultas";

    private const string ContarDocentes =
        "SELECT count(*) AS cantidad FROM designaciones.designaciones";

    // ------------------------------------------------------------- el permiso

    [Fact]
    public async Task Sin_identidad_el_endpoint_rechaza()
    {
        await SembrarAsync();
        using var host = CrearHost(out _);
        using var cliente = host.CreateClient();

        using var respuesta = await Preguntar(cliente, "¿cuántos docentes hay?");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Sin_el_permiso_del_asistente_el_endpoint_rechaza()
    {
        // `docente` es el único rol de sistema al que la siembra NO le concede
        // `asistente.consultar`. La exclusión es provisional y se revierte desde
        // /membresia-roles, sin migración.
        await SembrarAsync();
        using var host = CrearHost(out var proveedor);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Docente, "docente");

        using var respuesta = await Preguntar(cliente, "¿cuántos docentes hay?");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);

        // Y no gastó nada: el rechazo es antes del pipeline.
        Assert.Equal(0, proveedor.Llamadas);
    }

    // -------------------------------------------------------- la idempotencia

    [Fact]
    public async Task Sin_la_clave_de_idempotencia_el_pedido_se_rechaza_nombrandola()
    {
        await SembrarAsync();
        using var host = CrearHost(out _);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        using var respuesta = await Preguntar(cliente, "¿cuántos docentes hay?", clave: null);
        var cuerpo = await respuesta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains(
            AsistenteController.CabeceraDeIdempotencia, cuerpo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Una_clave_en_blanco_no_cuenta_como_clave()
    {
        await SembrarAsync();
        using var host = CrearHost(out _);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        using var respuesta = await Preguntar(cliente, "¿cuántos docentes hay?", clave: "   ");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_pedido_repetido_devuelve_lo_mismo_sin_volver_a_llamar_al_proveedor()
    {
        // Cada turno cuesta dos o tres llamadas al modelo: un doble submit se factura
        // completo dos veces. Se cuenta contra el proveedor y no contra el cuerpo de
        // la respuesta, porque devolver lo mismo habiendo gastado igual no resuelve
        // nada de lo que este requisito existe para resolver.
        await SembrarAsync();
        using var host = CrearHost(out var proveedor);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var clave = Guid.NewGuid().ToString();

        var primera = await LeerAsync(await Preguntar(cliente, "¿cuántos docentes hay?", clave));
        var gastadas = proveedor.Llamadas;

        var segunda = await LeerAsync(await Preguntar(cliente, "¿cuántos docentes hay?", clave));

        Assert.Equal(primera.Respuesta, segunda.Respuesta);
        Assert.Equal(primera.Hilo, segunda.Hilo);
        Assert.Equal(gastadas, proveedor.Llamadas);
    }

    [Fact]
    public async Task Una_clave_distinta_si_procesa()
    {
        await SembrarAsync();
        using var host = CrearHost(out var proveedor);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        await Preguntar(cliente, "¿cuántos docentes hay?", Guid.NewGuid().ToString());
        var gastadas = proveedor.Llamadas;

        await Preguntar(cliente, "¿cuántos docentes hay?", Guid.NewGuid().ToString());

        Assert.True(proveedor.Llamadas > gastadas);
    }

    [Fact]
    public async Task La_clave_de_un_actor_no_le_sirve_a_otro()
    {
        // Sin acotar por actor, la clave de un usuario le devolvería a otro una
        // respuesta calculada con el alcance del primero: una fuga trivial de
        // disparar y difícil de notar, porque el segundo recibe algo que parece
        // correcto.
        await SembrarAsync();
        using var host = CrearHost(out var proveedor);
        var clave = Guid.NewGuid().ToString();

        using var deSecretaria = host.CreateClient();
        Autenticar(deSecretaria, Secretaria, "secretaria");
        await Preguntar(deSecretaria, "¿cuántos docentes hay?", clave);
        var gastadas = proveedor.Llamadas;

        using var deCoordinador = host.CreateClient();
        Autenticar(deCoordinador, Coordinador, "coordinador_carrera");
        var segunda = await LeerAsync(
            await Preguntar(deCoordinador, "¿cuántos docentes hay?", clave));

        Assert.True(proveedor.Llamadas > gastadas,
            "La clave del primer actor le devolvió al segundo una respuesta ya calculada.");
        Assert.NotEqual(Guid.Empty, segunda.Hilo);
    }

    [Fact]
    public void La_caducidad_de_la_clave_devuelve_a_procesar_el_turno()
    {
        // La caché es en memoria y con expiración corta, así que la caducidad se
        // prueba donde vive el reloj y no por HTTP: mover el tiempo de un Host
        // levantado exigiría inyectarle un TimeProvider a todo el proceso.
        var reloj = new RelojFijo(new DateTimeOffset(2026, 8, 25, 10, 0, 0, TimeSpan.Zero));
        var idempotencia = new IdempotenciaEnMemoria(
            Microsoft.Extensions.Options.Options.Create(
                new OpcionesAsistente { VigenciaDeIdempotenciaMinutos = 5 }),
            reloj);

        var turno = TurnoCualquiera();
        idempotencia.Guardar(Secretaria, "k", turno);

        Assert.NotNull(idempotencia.Recordar(Secretaria, "k"));

        reloj.Avanzar(TimeSpan.FromMinutes(6));

        Assert.Null(idempotencia.Recordar(Secretaria, "k"));
    }

    [Fact]
    public void La_idempotencia_no_persiste_ninguna_fila()
    {
        // El requisito no es «que ande»: es que NO se copie
        // designaciones.idempotencia_comandos, que guarda el cuerpo completo de la
        // respuesta HTTP — exactamente lo que este módulo decidió no persistir.
        var tipo = typeof(IdempotenciaEnMemoria);

        Assert.DoesNotContain(
            tipo.GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Select(p => p.ParameterType.Name),
            nombre => nombre.Contains("Cadena", StringComparison.Ordinal));
    }

    // ------------------------------------------------------- lo que devuelve

    [Fact]
    public async Task La_respuesta_trae_el_estado_el_hilo_y_las_metricas()
    {
        await SembrarAsync();
        using var host = CrearHost(out _);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var cuerpo = await LeerAsync(
            await Preguntar(cliente, "¿cuántos docentes hay?", Guid.NewGuid().ToString()));

        Assert.Equal("respondida", cuerpo.Estado);
        Assert.NotEqual(Guid.Empty, cuerpo.Hilo);
        Assert.Equal(2, cuerpo.Metricas.LlamadasAlModelo);
        Assert.Null(cuerpo.Sql);
    }

    [Fact]
    public async Task La_consulta_que_el_hilo_arrastra_no_sale_por_la_API()
    {
        // `ResultadoDelTurno` lleva DOS campos con el mismo texto y distinta
        // pregunta: `Sql` es «¿esto se le puede MOSTRAR?» y depende de
        // `asistente.ver_consulta`; `SqlEjecutado` es «¿esto sirve para continuar la
        // conversación?» y no depende de ningún permiso porque nunca sale del
        // servidor.
        //
        // El día que alguien mapee el segundo al DTO «por simetría», la consulta
        // generada se le publica a todo actor sin el permiso, y nada más falla. Se
        // afirma sobre el JSON CRUDO y no sobre el DTO tipado: un campo nuevo en el
        // contrato aparece en el JSON aunque el DTO del test no lo declare.
        await SembrarAsync();
        using var host = CrearHost(out _);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var respuesta = await Preguntar(
            cliente, "¿cuántos docentes hay?", Guid.NewGuid().ToString());
        var json = await respuesta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain("sqlEjecutado", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task El_estado_del_contrato_no_es_el_nombre_del_enum()
    {
        // El nombre del enum es un detalle interno del backend: renombrarlo no puede
        // romper a los clientes en silencio.
        await SembrarAsync();
        using var host = CrearHost(out _);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var cuerpo = await LeerAsync(
            await Preguntar(cliente, "hola", Guid.NewGuid().ToString()));

        Assert.Equal("respondida", cuerpo.Estado);
        Assert.DoesNotContain("Respondida", cuerpo.Estado, StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_endpoint_de_capacidades_responde_por_actor()
    {
        await SembrarAsync();
        using var host = CrearHost(out var proveedor);

        using var deSecretaria = host.CreateClient();
        Autenticar(deSecretaria, Secretaria, "secretaria");

        using var deCoordinador = host.CreateClient();
        Autenticar(deCoordinador, Coordinador, "coordinador_carrera");

        var ct = TestContext.Current.CancellationToken;
        var global = await deSecretaria.GetFromJsonAsync<CapacidadesDto>(
            "/api/asistente/capacidades", ct);
        var acotado = await deCoordinador.GetFromJsonAsync<CapacidadesDto>(
            "/api/asistente/capacidades", ct);

        Assert.NotNull(global);
        Assert.NotNull(acotado);
        Assert.True(global.Columnas > acotado.Columnas);
        Assert.NotEqual(global.Alcance, acotado.Alcance);
        Assert.NotEmpty(acotado.Ejemplos);

        // Cero tokens: el catálogo sale de la base, no del modelo.
        Assert.Equal(0, proveedor.Llamadas);
    }

    // -------------------------------------------------- la conversación (D13)

    [Fact]
    public async Task Un_turno_persistido_nombra_la_conversacion_que_lista_GET_historial()
    {
        await SembrarAsync();
        using var host = CrearHost(out _);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var cuerpo = await LeerAsync(
            await Preguntar(cliente, "¿cuántos docentes hay?", Guid.NewGuid().ToString()));

        Assert.NotNull(cuerpo.Conversacion);

        var ct = TestContext.Current.CancellationToken;
        var historial = await cliente.GetFromJsonAsync<List<ConversacionResumenDto>>(
            "/api/asistente/historial", ct);

        Assert.NotNull(historial);
        Assert.Contains(historial, c => c.Id == cuerpo.Conversacion);
    }

    [Fact]
    public async Task Si_la_escritura_del_historial_falla_la_respuesta_no_nombra_ninguna_conversacion()
    {
        // Se reemplaza `IRegistroDeHistorial` por uno que nunca escribe, en vez
        // de forzar un error real de Postgres: `RegistroDeHistorial` ya se
        // traga esa excepción y simplemente no fija `HiloHistorico` (ver su
        // propio catch), así que un doble que no lo fija reproduce EXACTAMENTE
        // el mismo estado que ve `CapaConversacional` cuando la escritura
        // falla, sin acoplar este test a cómo se rompe una conexión.
        await SembrarAsync();
        using var host = CrearHost(out _, historialQueNuncaEscribe: true);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var cuerpo = await LeerAsync(
            await Preguntar(cliente, "¿cuántos docentes hay?", Guid.NewGuid().ToString()));

        Assert.Null(cuerpo.Conversacion);
    }

    [Fact]
    public async Task La_conversacion_no_se_guarda_junto_al_registro_analitico_ni_a_la_retroalimentacion()
    {
        // asistente.registro_analitico y asistente.retroalimentacion_turno son,
        // a propósito, las dos tablas que TD-012 mantiene sin nada que las
        // vincule a una conversación o a un actor identificable — agregarle
        // una columna a cualquiera de las dos reabriría exactamente el cruce
        // que design.md D13 promete no crear.
        await SembrarAsync();

        var columnas = await EscalarAsync<long>(
            """
            SELECT count(*)
              FROM information_schema.columns
             WHERE table_schema = 'asistente'
               AND table_name IN ('registro_analitico', 'retroalimentacion_turno')
               AND column_name ILIKE '%conversacion%'
            """);

        Assert.Equal(0, columnas);
    }

    [Fact]
    public async Task El_actor_sale_de_la_sesion_y_no_del_cuerpo_del_pedido()
    {
        // El pedido no tiene dónde poner un actor, y ése es el punto: un
        // identificador tomado del cuerpo sería un selector de alcance controlado por
        // el cliente.
        Assert.DoesNotContain(
            typeof(ConsultaDelAsistente).GetProperties().Select(p => p.Name),
            nombre => nombre.Contains("Actor", StringComparison.OrdinalIgnoreCase)
                || nombre.Contains("Usuario", StringComparison.OrdinalIgnoreCase));

        await Task.CompletedTask;
    }

    // --------------------------------------- reemplazo (design.md D9, ARS-147)

    [Fact]
    public async Task Reemplazar_una_pregunta_que_no_es_la_ultima_devuelve_409()
    {
        await SembrarAsync();
        using var host = CrearHost(out _);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var primera = await LeerAsync(
            await Preguntar(cliente, "¿cuántos docentes hay?", "clave-1"));
        await LeerAsync(await Preguntar(
            cliente, "¿y en Álgebra?", "clave-2", hilo: primera.Hilo));

        using var respuesta = await Preguntar(
            cliente, "¿y en Análisis?", "clave-3", hilo: primera.Hilo, reemplaza: "clave-1");

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    [Fact]
    public async Task Reemplazar_sobre_un_hilo_que_no_existe_devuelve_409_y_no_escribe_nada()
    {
        await SembrarAsync();
        using var host = CrearHost(out var proveedor);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        using var respuesta = await Preguntar(
            cliente, "¿y en Análisis?", "clave-1",
            hilo: Guid.NewGuid(), reemplaza: "cualquier-clave");

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal(0, proveedor.Llamadas);
    }

    [Fact]
    public async Task Un_reemplazo_valido_actualiza_la_conversacion_persistida()
    {
        await SembrarAsync();
        using var host = CrearHost(out _);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");
        var ct = TestContext.Current.CancellationToken;

        var primera = await LeerAsync(
            await Preguntar(cliente, "¿cuántos docentes hay?", "clave-1"));

        var segunda = await LeerAsync(await Preguntar(
            cliente, "¿cuántos adjuntos hay?", "clave-2", hilo: primera.Hilo, reemplaza: "clave-1"));

        Assert.Equal(primera.Hilo, segunda.Hilo);
        Assert.Equal(primera.Conversacion, segunda.Conversacion);

        var detalle = await cliente.GetFromJsonAsync<ConversacionDetalleDto>(
            $"/api/asistente/historial/{segunda.Conversacion}", ct);

        Assert.NotNull(detalle);
        var turnoFinal = Assert.Single(detalle.Turnos);
        Assert.Contains("adjuntos", turnoFinal.Pregunta, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task El_reemplazo_revoca_el_token_viejo_y_acepta_el_nuevo()
    {
        await SembrarAsync();
        using var host = CrearHost(out _);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");
        var ct = TestContext.Current.CancellationToken;

        var primera = await LeerAsync(
            await Preguntar(cliente, "¿cuántos docentes hay?", "clave-1"));
        var tokenViejo = primera.ClaveDeRetroalimentacion;
        Assert.NotNull(tokenViejo);

        var segunda = await LeerAsync(await Preguntar(
            cliente, "¿cuántos adjuntos hay?", "clave-2", hilo: primera.Hilo, reemplaza: "clave-1"));

        using var rechazado = await cliente.PostAsJsonAsync(
            "/api/asistente/retroalimentacion",
            new PedidoDeRetroalimentacion(tokenViejo!.Value, true, null),
            ct);
        Assert.Equal(HttpStatusCode.NotFound, rechazado.StatusCode);

        using var aceptado = await cliente.PostAsJsonAsync(
            "/api/asistente/retroalimentacion",
            new PedidoDeRetroalimentacion(segunda.ClaveDeRetroalimentacion!.Value, true, null),
            ct);
        Assert.Equal(HttpStatusCode.NoContent, aceptado.StatusCode);
    }

    [Fact]
    public async Task Un_reintento_con_la_misma_clave_aplica_el_reemplazo_una_sola_vez()
    {
        // «quota charged once even on retry with the same Idempotency-Key»: la
        // segunda vez con la misma clave y el mismo objetivo tiene que devolver
        // lo mismo sin volver a llamar al proveedor ni reemplazar de nuevo.
        await SembrarAsync();
        using var host = CrearHost(out var proveedor);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");
        var ct = TestContext.Current.CancellationToken;

        var primera = await LeerAsync(
            await Preguntar(cliente, "¿cuántos docentes hay?", "clave-1"));

        var segunda = await LeerAsync(await Preguntar(
            cliente, "¿cuántos adjuntos hay?", "clave-2", hilo: primera.Hilo, reemplaza: "clave-1"));
        var gastadas = proveedor.Llamadas;

        var reintento = await LeerAsync(await Preguntar(
            cliente, "¿cuántos adjuntos hay?", "clave-2", hilo: primera.Hilo, reemplaza: "clave-1"));

        Assert.Equal(gastadas, proveedor.Llamadas);
        Assert.Equal(segunda.Respuesta, reintento.Respuesta);
        Assert.Equal(segunda.Conversacion, reintento.Conversacion);

        var detalle = await cliente.GetFromJsonAsync<ConversacionDetalleDto>(
            $"/api/asistente/historial/{segunda.Conversacion}", ct);

        Assert.NotNull(detalle);
        // Un solo turno final: el reintento no aplicó un segundo reemplazo.
        Assert.Single(detalle.Turnos);
    }

    // ------------------------------------------------ menciones (D10/D11, 7.2/7.3)

    private static readonly Guid MateriaAlgoritmos = Guid.Parse("70000000-0000-4000-8000-000000000102");
    private static readonly Guid MateriaDeIndustrial = Guid.Parse("70000000-0000-4000-8000-000000000201");

    [Fact]
    public async Task GET_menciones_devuelve_materias_dentro_del_alcance()
    {
        await SembrarAsync();
        using var host = CrearHost(out _);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        using var respuesta = await cliente.GetAsync(
            "/api/asistente/menciones?tipo=materia&q=algoritmos", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<MencionesDto>(
            TestContext.Current.CancellationToken);

        Assert.NotNull(cuerpo);
        Assert.Contains(cuerpo!.Resultados, r => r.Id == MateriaAlgoritmos);
    }

    [Fact]
    public async Task GET_menciones_con_menos_de_dos_letras_es_400()
    {
        await SembrarAsync();
        using var host = CrearHost(out _);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        using var respuesta = await cliente.GetAsync(
            "/api/asistente/menciones?tipo=materia&q=a", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task GET_menciones_con_un_tipo_desconocido_es_400()
    {
        await SembrarAsync();
        using var host = CrearHost(out _);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        using var respuesta = await cliente.GetAsync(
            "/api/asistente/menciones?tipo=alumno&q=algoritmos", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Una_referencia_fuera_de_alcance_es_400_sin_gastar_cupo_ni_historial()
    {
        await SembrarAsync();
        using var host = CrearHost(out var proveedor);
        using var cliente = host.CreateClient();
        // Coordinador: ámbito de Ingeniería en Informática. La materia
        // referenciada es de Ingeniería Industrial.
        Autenticar(cliente, Coordinador, "coordinador_carrera");

        using var respuesta = await Preguntar(
            cliente, "¿quién la dicta?",
            referencias: [new ReferenciaDto("materia", MateriaDeIndustrial)]);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(0, proveedor.Llamadas);
    }

    [Fact]
    public async Task Una_referencia_a_un_id_inexistente_da_el_mismo_400_que_una_fuera_de_alcance()
    {
        // Sin oráculo de existencia (design.md D11): mismo código y mismo cuerpo
        // para las dos causas.
        await SembrarAsync();
        using var host = CrearHost(out _);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Coordinador, "coordinador_carrera");

        var fueraDeAlcance = await Preguntar(
            cliente, "¿quién la dicta?", referencias: [new ReferenciaDto("materia", MateriaDeIndustrial)]);
        var inexistente = await Preguntar(
            cliente, "¿quién la dicta?", referencias: [new ReferenciaDto("materia", Guid.NewGuid())]);

        Assert.Equal(HttpStatusCode.BadRequest, fueraDeAlcance.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, inexistente.StatusCode);

        var cuerpoFueraDeAlcance = await fueraDeAlcance.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        var cuerpoInexistente = await inexistente.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(cuerpoFueraDeAlcance, cuerpoInexistente);
    }

    [Fact]
    public async Task Un_tipo_de_referencia_desconocido_es_400_como_cualquier_referencia_invalida()
    {
        await SembrarAsync();
        using var host = CrearHost(out _);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        using var respuesta = await Preguntar(
            cliente, "¿quién la dicta?", referencias: [new ReferenciaDto("alumno", MateriaAlgoritmos)]);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Mas_de_cinco_referencias_es_400_antes_de_resolver_nada()
    {
        await SembrarAsync();
        using var host = CrearHost(out var proveedor);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var seis = Enumerable.Range(0, 6)
            .Select(_ => new ReferenciaDto("materia", MateriaAlgoritmos))
            .ToArray();

        using var respuesta = await Preguntar(cliente, "¿quién la dicta?", referencias: seis);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(0, proveedor.Llamadas);
    }

    [Fact]
    public async Task Una_referencia_valida_dentro_del_alcance_no_se_rechaza()
    {
        await SembrarAsync();
        using var host = CrearHost(out _);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        using var respuesta = await Preguntar(
            cliente, "¿quién la dicta?", referencias: [new ReferenciaDto("materia", MateriaAlgoritmos)]);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    // ------------------------------------------------------------------ apoyo

    private static Modules.Asistente.Application.ResultadoDelTurno TurnoCualquiera() =>
        new(Modules.Asistente.Application.EstadoDelTurno.Respondida,
            "listo",
            Razonamiento: string.Empty,
            PreguntaInterpretada: null,
            [],
            [],
            Truncado: false,
            [],
            "consulta_simple",
            LlamadasAlModelo: 0,
            Guid.NewGuid());

    private static void Autenticar(HttpClient cliente, Guid usuario, string rol)
    {
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, usuario.ToString());
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, rol);
    }

    private static async Task<HttpResponseMessage> Preguntar(
        HttpClient cliente,
        string mensaje,
        string? clave = "clave-de-prueba",
        Guid? hilo = null,
        string? reemplaza = null,
        IReadOnlyList<ReferenciaDto>? referencias = null)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Post, Ruta)
        {
            Content = JsonContent.Create(new ConsultaDelAsistente(mensaje, hilo, reemplaza, referencias)),
        };

        if (clave is not null)
        {
            pedido.Headers.TryAddWithoutValidation(
                AsistenteController.CabeceraDeIdempotencia, clave);
        }

        return await cliente.SendAsync(pedido, TestContext.Current.CancellationToken);
    }

    private static async Task<RespuestaDelAsistente> LeerAsync(HttpResponseMessage respuesta)
    {
        var ct = TestContext.Current.CancellationToken;
        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);

        Assert.True(respuesta.IsSuccessStatusCode, cuerpo);

        return (await respuesta.Content.ReadFromJsonAsync<RespuestaDelAsistente>(ct))!;
    }

    private WebApplicationFactory<Program> CrearHost(
        out ProveedorGuionado proveedor, bool historialQueNuncaEscribe = false)
    {
        // Guion largo: cada turno del carril consume generación + redacción, y el
        // proveedor guionado repite su última respuesta al agotarse. Un guion corto
        // haría que un turno de más terminara no contestable por una razón que no es
        // la que el test mide.
        var guion = Enumerable.Range(0, 12).SelectMany(_ => new[]
        {
            ProveedorGuionado.Generacion(ContarDocentes),
            "Hay 4 docentes designados.",
        }).ToArray();

        var guionado = new ProveedorGuionado(guion);
        proveedor = guionado;

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


            // El proveedor guionado reemplaza al simulado: contar sus llamadas es lo
            // único que prueba de verdad que la idempotencia no volvió a gastar.
            builder.ConfigureTestServices(servicios =>
            {
                servicios.AddSingleton(new ProveedorBase(guionado));

                if (historialQueNuncaEscribe)
                {
                    // Registrado DESPUÉS del `AddScoped<IRegistroDeHistorial,
                    // RegistroDeHistorial>` del módulo: el contenedor resuelve
                    // la última registración, así que esto lo reemplaza sin
                    // tocar `ModuleExtensions`.
                    servicios.AddScoped<
                        Modules.Asistente.Application.IRegistroDeHistorial, HistorialQueNuncaEscribe>();
                }
            });
        });
    }

    /// <summary>
    /// Un <see cref="Modules.Asistente.Application.IRegistroDeHistorial"/> que
    /// nunca escribe nada — el mismo estado observable que el real deja cuando
    /// la escritura falla y se traga la excepción (nunca fija
    /// <c>HiloConversacional.HiloHistorico</c>), sin depender de reproducir un
    /// error real de Postgres.
    /// </summary>
    private sealed class HistorialQueNuncaEscribe : Modules.Asistente.Application.IRegistroDeHistorial
    {
        public Task RegistrarTurnoAsync(
            Modules.Asistente.Application.HiloConversacional conversacion,
            Modules.Asistente.Application.TurnoParaHistorial turno,
            CancellationToken ct) => Task.CompletedTask;

        public Task ReemplazarUltimoTurnoAsync(
            Modules.Asistente.Application.HiloConversacional conversacion,
            Modules.Asistente.Application.TurnoParaHistorial turno,
            CancellationToken ct) => Task.CompletedTask;
    }

}
