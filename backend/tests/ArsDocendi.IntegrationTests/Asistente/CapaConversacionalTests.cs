using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>Verifica la capa conversacional de punta a punta, contra una base real.</summary>
public sealed class CapaConversacionalTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_conversacion", sembrada: true)
{
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    private static readonly Guid Coordinador = Guid.Parse("a0000000-0000-4000-8000-000000000003");
    private const string Industrial = "c0000000-0000-4000-8000-000000000202";

    private const string ContarDocentes =
        "SELECT count(*) AS cantidad FROM designaciones.designaciones";

    // ------------------------------------------------------ carril sin datos

    [Theory]
    [InlineData("hola")]
    [InlineData("gracias")]
    [InlineData("¿qué podés hacer?")]
    public async Task Un_mensaje_social_no_llama_al_modelo(string mensaje)
    {
        var banco = Banco();

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, mensaje, TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.Respondida, turno.Estado);
        Assert.Equal(0, turno.LlamadasAlModelo);
        Assert.Equal(0, banco.Proveedor.Llamadas);
        Assert.NotEqual(Guid.Empty, turno.Hilo);
    }

    [Fact]
    public async Task Una_pregunta_con_apertura_cortes_sigue_al_carril()
    {
        var banco = Banco(ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.");

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "hola, ¿cuántos docentes están designados?",
            TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.Respondida, turno.Estado);
        Assert.Equal(2, banco.Proveedor.Llamadas);
    }

    // -------------------------------------------------------- seguimiento

    [Fact]
    public async Task El_primer_turno_no_paga_la_reescritura()
    {
        var banco = Banco(ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.");

        await banco.Capa().ResponderAsync(
            Secretaria, null, "¿Cuántos docentes están designados?",
            TestContext.Current.CancellationToken);

        // Generación y redacción, y nada más. Sin historial no hay nada que
        // reescribir, así que esa llamada no se hace.
        Assert.Equal(2, banco.Proveedor.Llamadas);
        Assert.DoesNotContain(
            "Anterior:", banco.Proveedor.Recibidas[0].Mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_seguimiento_paga_la_reescritura_y_le_manda_el_turno_anterior()
    {
        var ct = TestContext.Current.CancellationToken;
        var banco = Banco(ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.");

        var primero = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿Cuántos docentes están designados en Bases de Datos?", ct);

        var seguimiento = Banco(
            banco.Hilos,
            "¿Cuántos docentes están designados en Álgebra?",
            ProveedorGuionado.Generacion(ContarDocentes),
            "Hay 2 docentes.");

        await seguimiento.Capa().ResponderAsync(Secretaria, primero.Hilo, "¿y en Álgebra?", ct);

        // Reescritura, generación y redacción.
        Assert.Equal(3, seguimiento.Proveedor.Llamadas);

        var reescritura = seguimiento.Proveedor.Recibidas[0].Mensaje;
        Assert.Contains("Anterior:", reescritura, StringComparison.Ordinal);
        Assert.Contains("Bases de Datos", reescritura, StringComparison.Ordinal);
    }

    [Fact]
    public async Task En_el_pivote_no_se_le_manda_ningun_turno_anterior()
    {
        // EL TEST DEL CAMBIO DE TEMA. No mira la salida del modelo: mira qué se le mandó.
        var ct = TestContext.Current.CancellationToken;
        var banco = Banco(ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.");

        var primero = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿Cuántos docentes están designados en Bases de Datos?", ct);

        var pivote = Banco(banco.Hilos, ProveedorGuionado.Generacion(ContarDocentes), "Hay 6.");

        var turno = await pivote.Capa().ResponderAsync(
            Secretaria, primero.Hilo, "¿Qué materias tiene Organización Industrial?", ct);

        Assert.All(
            pivote.Proveedor.Recibidas,
            solicitud => Assert.DoesNotContain(
                "Anterior:", solicitud.Mensaje, StringComparison.Ordinal));

        // En el pivote la pregunta interpretada se devuelve siempre: es la señal de
        // que el asistente soltó el tema anterior.
        Assert.NotNull(turno.PreguntaInterpretada);
    }

    // -------------------------------------------------------- aclaración

    [Fact]
    public async Task Una_pregunta_ambigua_pide_aclaracion_sin_llamar_al_modelo()
    {
        await AgregarColisionesAsync();
        var banco = Banco();

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿Quiénes dan Bases de Datos?",
            TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.NecesitaAclaracion, turno.Estado);
        Assert.Equal(2, turno.Opciones!.Count);
        Assert.Equal(0, banco.Proveedor.Llamadas);
    }

    [Fact]
    public async Task Elegir_una_opcion_resuelve_la_pregunta()
    {
        await AgregarColisionesAsync();
        var ct = TestContext.Current.CancellationToken;
        var banco = Banco();

        var menu = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿Quiénes dan Bases de Datos?", ct);

        var respuesta = Banco(banco.Hilos, ProveedorGuionado.Generacion(ContarDocentes), "Hay 2.");
        var turno = await respuesta.Capa().ResponderAsync(Secretaria, menu.Hilo, "1", ct);

        Assert.Equal(EstadoDelTurno.Respondida, turno.Estado);

        // El ordinal llegó convertido en etiqueta: lo que sigue vio la opción
        // canónica, no el «1» que el usuario tipeó.
        Assert.Contains("Me refiero a", turno.PreguntaInterpretada!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Con_una_aclaracion_pendiente_un_agradecimiento_no_la_interrumpe()
    {
        // Sin la guarda, «gracias» le robaría la respuesta al menú abierto y la
        // aclaración quedaría colgada.
        await AgregarColisionesAsync();
        var ct = TestContext.Current.CancellationToken;
        var banco = Banco();

        var menu = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿Quiénes dan Bases de Datos?", ct);

        var segundo = Banco(banco.Hilos);
        var turno = await segundo.Capa().ResponderAsync(Secretaria, menu.Hilo, "gracias", ct);

        // No se resolvió como agradecimiento: se trató como respuesta al menú, no
        // se reconoció, y el menú vuelve.
        Assert.Equal(EstadoDelTurno.NecesitaAclaracion, turno.Estado);
    }

    [Fact]
    public async Task Al_agotar_los_intentos_la_aclaracion_se_abandona()
    {
        await AgregarColisionesAsync();
        var ct = TestContext.Current.CancellationToken;
        var banco = Banco();

        var menu = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿Quiénes dan Bases de Datos?", ct);

        var reintento = Banco(banco.Hilos);
        await reintento.Capa().ResponderAsync(Secretaria, menu.Hilo, "ni idea", ct);
        var ultimo = await reintento.Capa().ResponderAsync(Secretaria, menu.Hilo, "tampoco", ct);

        Assert.Equal(EstadoDelTurno.NoContestable, ultimo.Estado);

        // Y sobre todo: el hilo quedó libre, no colgado esperando una respuesta que
        // nunca llega.
        var libre = banco.Hilos.Resolver(menu.Hilo, Secretaria);
        Assert.Null(libre.AclaracionPendiente);
    }

    // -------------------------------------------------------------- el hilo

    [Fact]
    public async Task Sin_hilo_una_pregunta_autocontenida_se_responde_igual()
    {
        var banco = Banco(ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.");

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, Guid.NewGuid(), "¿Cuántos docentes están designados?",
            TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.Respondida, turno.Estado);
    }

    [Fact]
    public async Task Un_hilo_ajeno_se_rechaza()
    {
        var ct = TestContext.Current.CancellationToken;
        var banco = Banco(ProveedorGuionado.Generacion(ContarDocentes), "Hay 4.");

        var deSecretaria = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿Cuántos docentes están designados?", ct);

        await Assert.ThrowsAsync<HiloAjeno>(
            () => Banco(banco.Hilos).Capa().ResponderAsync(
                Coordinador, deSecretaria.Hilo, "¿y ahora?", ct));
    }

    [Fact]
    public async Task El_turno_acumula_en_el_hilo()
    {
        var ct = TestContext.Current.CancellationToken;
        var banco = Banco(ProveedorGuionado.Generacion(ContarDocentes), "Hay 4.");

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿Cuántos docentes están designados?", ct);

        var hilo = banco.Hilos.Resolver(turno.Hilo, Secretaria);

        Assert.Single(hilo.Turnos);
        Assert.Contains("docentes", hilo.Turnos[0].Pregunta, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task El_hilo_no_guarda_ningun_valor_de_las_filas()
    {
        // La misma restricción que el enmascarador impuso en la salida, sostenida en el historial:
        // si el hilo guardara filas, los datos personales volverían al prompt por la puerta del historial.
        var ct = TestContext.Current.CancellationToken;
        var banco = Banco(
            ProveedorGuionado.Generacion(
                "SELECT apellido, documento FROM identity.personas ORDER BY legajo"),
            "Encontré varias personas.");

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿Qué documentos hay cargados?", ct);

        var hilo = banco.Hilos.Resolver(turno.Hilo, Secretaria);

        // RECORRE TODO LO QUE EL TURNO GUARDA, no sólo la pregunta. Miraba sólo
        // `Pregunta`, y el día que el hilo sumó la consulta ejecutada este test
        // habría seguido en verde con un valor de fila adentro del SQL. Un test que
        // filtra lo que revisa deja de proteger sin dejar de pasar.
        var todo = string.Join(
            "\n", hilo.Turnos.Select(t => $"{t.Pregunta}\n{t.SqlEjecutado}"));

        Assert.DoesNotContain("28341567", todo, StringComparison.Ordinal);
        Assert.DoesNotContain("López", todo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_hilo_arrastra_la_consulta_que_respondio()
    {
        var ct = TestContext.Current.CancellationToken;
        var banco = Banco(ProveedorGuionado.Generacion(ContarDocentes), "Hay 4.");

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿Cuántos docentes están designados?", ct);

        var hilo = banco.Hilos.Resolver(turno.Hilo, Secretaria);

        Assert.Equal(ContarDocentes, hilo.Turnos[0].SqlEjecutado);
    }

    [Fact]
    public async Task Un_turno_sin_filas_no_deja_consulta_para_arrastrar()
    {
        // Ofrecerle al modelo una consulta que no encontró nada lo invita a
        // repetirla, y el turno siguiente heredaría el error del anterior.
        var ct = TestContext.Current.CancellationToken;
        var banco = Banco(
            ProveedorGuionado.Generacion(
                "SELECT numero FROM designaciones.pedidos WHERE numero = 'no-existe'"),
            ProveedorGuionado.Generacion(
                "SELECT numero FROM designaciones.pedidos WHERE numero = 'tampoco'"));

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿Existe el trámite no-existe?", ct);

        var hilo = banco.Hilos.Resolver(turno.Hilo, Secretaria);

        Assert.Null(hilo.Turnos[0].SqlEjecutado);
    }

    [Fact]
    public async Task Un_seguimiento_recibe_la_consulta_del_turno_anterior()
    {
        // EL CASO QUE MOTIVÓ EL CAMBIO. «esa materia» se refiere a algo que apareció en la
        // RESPUESTA, nunca en una pregunta: con sólo preguntas en el hilo no hay forma de resolverlo.
        var ct = TestContext.Current.CancellationToken;
        var banco = Banco(
            ProveedorGuionado.Generacion(ContarDocentes),
            "Hay 4 docentes.",
            "¿y los profesores de esa materia?",
            ProveedorGuionado.Generacion(ContarDocentes),
            "Ahí van.");

        var primero = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿Cuántos docentes están designados?", ct);

        await banco.Capa().ResponderAsync(
            Secretaria, primero.Hilo, "¿y los profesores de esa materia?", ct);

        // La generación del segundo turno tiene que haber visto la consulta del primero.
        var generaciones = banco.Proveedor.Recibidas
            .Where(s => s.Mensaje.Contains("Fecha de referencia", StringComparison.Ordinal))
            .ToList();

        Assert.Contains(ContarDocentes, generaciones[^1].Mensaje, StringComparison.Ordinal);
        Assert.DoesNotContain(
            ContarDocentes, generaciones[0].Mensaje, StringComparison.Ordinal);
    }

    // ------------------------- el seguimiento cuya referencia no se resolvió

    [Fact]
    public async Task Un_seguimiento_sin_resolver_dice_que_no_entendio_a_que_se_referia()
    {
        // El reescritor promete «una pregunta que se entienda sola» y a veces devuelve algo que no
        // se entiende solo.
        var ct = TestContext.Current.CancellationToken;
        var banco = Banco(
            ProveedorGuionado.Generacion(ContarDocentes),
            "Hay 4 docentes.",
            "¿los profesores de esa materia?",
            ProveedorGuionado.NoContestable());

        var primero = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿Cuántos docentes están designados?", ct);

        var segundo = await banco.Capa().ResponderAsync(
            Secretaria, primero.Hilo, "¿los profesores de esa materia?", ct);

        Assert.Equal(EstadoDelTurno.NoContestable, segundo.Estado);
        Assert.Equal(PoliticaDeAbstencion.TextoReferenciaSinResolver, segundo.Respuesta);
    }

    // -------------------------------------- escalación de rechazos (grupo 6.2)

    [Fact]
    public async Task Una_referencia_sin_resolver_pisa_el_texto_aunque_ya_hubo_un_rechazo_antes()
    {
        // La precedencia de CierreDelTurno.TextoDelRechazo se sostiene aunque la
        // escalación ya esté en la segunda variante: el diagnóstico de
        // referencia sin resolver es más específico que el motivo del modelo.
        var ct = TestContext.Current.CancellationToken;
        var banco = Banco(
            ProveedorGuionado.NoContestable(),
            "¿los profesores de esa materia?",
            ProveedorGuionado.NoContestable());

        var primero = await banco.Capa().ResponderAsync(Secretaria, null, "¿algo raro?", ct);
        var segundo = await banco.Capa().ResponderAsync(
            Secretaria, primero.Hilo, "¿los profesores de esa materia?", ct);

        Assert.Equal(EstadoDelTurno.NoContestable, segundo.Estado);
        Assert.Equal(PoliticaDeAbstencion.TextoReferenciaSinResolver, segundo.Respuesta);
    }

    [Fact]
    public async Task Un_seguimiento_con_demostrativo_que_se_resuelve_no_cambia_nada()
    {
        // Mientras el generador pueda contestar —y con el arrastre muchas veces
        // puede— no hay nada que explicar.
        var ct = TestContext.Current.CancellationToken;
        var banco = Banco(
            ProveedorGuionado.Generacion(ContarDocentes),
            "Hay 4 docentes.",
            "¿los profesores de esa materia?",
            ProveedorGuionado.Generacion(ContarDocentes),
            "Ahí van.");

        var primero = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿Cuántos docentes están designados?", ct);

        var segundo = await banco.Capa().ResponderAsync(
            Secretaria, primero.Hilo, "¿los profesores de esa materia?", ct);

        Assert.Equal(EstadoDelTurno.Respondida, segundo.Estado);
        Assert.Equal("Ahí van.", segundo.Respuesta);
    }

    [Fact]
    public async Task Una_colision_termina_en_aclaracion()
    {
        await AgregarColisionesAsync();
        var banco = Banco(ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.");

        var turno = await banco.Capa().ResponderAsync(
            Secretaria,
            null,
            "¿cómo está el plantel de Bases de Datos?",
            TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.NecesitaAclaracion, turno.Estado);
        Assert.Equal(CarrilDelTurno.Aclaracion, ((RegistroEnMemoria)banco.Registro).Turnos.Single().Carril);
    }

    // --------------------------------------- reemplazo (design.md D9, ARS-147)

    [Fact]
    public async Task Reemplazar_el_ultimo_turno_lo_saca_del_hilo_y_agrega_el_nuevo()
    {
        var ct = TestContext.Current.CancellationToken;
        var banco = Banco(ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.");

        var primero = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿Cuántos docentes están designados?", ct, claveDelCliente: "clave-1");

        var segundo = Banco(banco.Hilos, ProveedorGuionado.Generacion(ContarDocentes), "Hay 2 adjuntos.");
        var reemplazo = await segundo.Capa().ResponderAsync(
            Secretaria, primero.Hilo, "¿Cuántos adjuntos hay?", ct,
            claveDelCliente: "clave-2", reemplaza: "clave-1");

        Assert.Equal(EstadoDelTurno.Respondida, reemplazo.Estado);

        var hilo = banco.Hilos.Resolver(primero.Hilo, Secretaria);
        var unico = Assert.Single(hilo.Turnos);
        Assert.Contains("adjuntos", unico.Pregunta, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("clave-2", unico.ClaveDelCliente);
    }

    [Fact]
    public async Task Reemplazar_un_turno_que_no_es_el_ultimo_es_rechazado_y_no_cambia_nada()
    {
        var ct = TestContext.Current.CancellationToken;
        var banco = Banco(ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.");

        var primero = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿Cuántos docentes están designados?", ct, claveDelCliente: "clave-1");

        var segundo = Banco(banco.Hilos, ProveedorGuionado.Generacion(ContarDocentes), "Hay 6.");
        var respondido = await segundo.Capa().ResponderAsync(
            Secretaria, primero.Hilo, "¿y en Álgebra?", ct, claveDelCliente: "clave-2");

        var tercero = Banco(banco.Hilos);
        await Assert.ThrowsAsync<ReemplazoInvalido>(
            () => tercero.Capa().ResponderAsync(
                Secretaria, respondido.Hilo, "¿y ahora?", ct,
                claveDelCliente: "clave-3", reemplaza: "clave-1"));

        var hilo = banco.Hilos.Resolver(primero.Hilo, Secretaria);
        Assert.Equal(2, hilo.Turnos.Count);
        Assert.Single(((HistorialEnMemoria)banco.Historial).Turnos);
    }

    [Fact]
    public async Task Reemplazar_un_hilo_vencido_es_rechazado()
    {
        var banco = Banco();

        await Assert.ThrowsAsync<ReemplazoInvalido>(
            () => banco.Capa().ResponderAsync(
                Secretaria, Guid.NewGuid(), "¿y ahora?", TestContext.Current.CancellationToken,
                claveDelCliente: "clave-1", reemplaza: "clave-vieja"));
    }

    [Fact]
    public async Task Un_reemplazo_rechazado_por_el_candado_no_cambia_nada()
    {
        // design.md D9, punto 4: el rechazo del candado no cambia nada — ni el
        // hilo, ni el historial.
        var ct = TestContext.Current.CancellationToken;
        var banco = Banco(ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.");

        var primero = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿Cuántos docentes están designados?", ct, claveDelCliente: "clave-1");

        var candado = await banco.CandadoDelTurno.IntentarAsync(Secretaria, ct);
        Assert.NotNull(candado);

        var rechazado = await banco.Capa().ResponderAsync(
            Secretaria, primero.Hilo, "¿y en Álgebra?", ct,
            claveDelCliente: "clave-2", reemplaza: "clave-1");

        Assert.Equal(EstadoDelTurno.ServicioDegradado, rechazado.Estado);

        var hilo = banco.Hilos.Resolver(primero.Hilo, Secretaria);
        Assert.Single(hilo.Turnos);
        Assert.Single(((HistorialEnMemoria)banco.Historial).Turnos);

        await candado!.DisposeAsync();
    }

    [Fact]
    public async Task El_reemplazo_revoca_el_token_viejo_y_mintea_uno_nuevo()
    {
        var ct = TestContext.Current.CancellationToken;
        var banco = Banco(ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.");

        var primero = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿Cuántos docentes están designados?", ct, claveDelCliente: "clave-1");
        var tokenViejo = primero.ClaveDeRetroalimentacion!.Value;

        Assert.True(banco.ValidezDeRetroalimentacion.EsVigente(tokenViejo, DateTimeOffset.UtcNow));

        var segundo = Banco(banco.Hilos, banco.Historial, banco.ValidezDeRetroalimentacion,
            ProveedorGuionado.Generacion(ContarDocentes), "Hay 2 adjuntos.");
        var reemplazo = await segundo.Capa().ResponderAsync(
            Secretaria, primero.Hilo, "¿Cuántos adjuntos hay?", ct,
            claveDelCliente: "clave-2", reemplaza: "clave-1");

        Assert.False(banco.ValidezDeRetroalimentacion.EsVigente(tokenViejo, DateTimeOffset.UtcNow));
        Assert.True(banco.ValidezDeRetroalimentacion.EsVigente(
            reemplazo.ClaveDeRetroalimentacion!.Value, DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task El_historial_conserva_solo_la_version_final()
    {
        var ct = TestContext.Current.CancellationToken;
        var banco = Banco(ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.");

        var primero = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿Cuántos docentes están designados?", ct, claveDelCliente: "clave-1");

        var segundo = Banco(banco.Hilos, banco.Historial, banco.ValidezDeRetroalimentacion,
            ProveedorGuionado.Generacion(ContarDocentes), "Hay 2 adjuntos.");
        await segundo.Capa().ResponderAsync(
            Secretaria, primero.Hilo, "¿Cuántos adjuntos hay?", ct,
            claveDelCliente: "clave-2", reemplaza: "clave-1");

        var fila = Assert.Single(((HistorialEnMemoria)banco.Historial).Turnos);
        Assert.Contains("adjuntos", fila.Pregunta, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Un_seguimiento_tras_el_reemplazo_usa_el_contexto_reemplazado()
    {
        // design.md D9: «un seguimiento tras un reemplazo usa el contexto reemplazado» — la
        // pregunta con anáfora que sigue al reemplazo tiene que ver, en la reescritura, la pregunta
        // y la consulta NUEVAS, nunca las reemplazadas.
        var ct = TestContext.Current.CancellationToken;
        var banco = Banco(ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.");

        var primero = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿Cuántos docentes están designados en Bases de Datos?", ct,
            claveDelCliente: "clave-1");

        var segundo = Banco(banco.Hilos, ProveedorGuionado.Generacion(ContarDocentes), "Hay 2 adjuntos.");
        var reemplazo = await segundo.Capa().ResponderAsync(
            Secretaria, primero.Hilo, "¿Cuántos adjuntos hay en Álgebra?", ct,
            claveDelCliente: "clave-2", reemplaza: "clave-1");

        var seguimiento = Banco(banco.Hilos, ProveedorGuionado.Generacion(ContarDocentes), "Hay 6.");
        await seguimiento.Capa().ResponderAsync(
            Secretaria, reemplazo.Hilo, "¿y en Sistemas?", ct, claveDelCliente: "clave-3");

        var reescritura = seguimiento.Proveedor.Recibidas[0].Mensaje;
        Assert.Contains("Álgebra", reescritura, StringComparison.Ordinal);
        Assert.DoesNotContain("Bases de Datos", reescritura, StringComparison.Ordinal);
    }

    // ----------------------- reemplazo sobre un último turno de otro carril (bug real: sólo el
    // carril SQL agrega al contexto vía `Agregar`, pero «Editar y reenviar» tiene que poder nombrar
    // CUALQUIER último turno registrado, sea cual sea su carril — design.md D9, fix del coordinador).

    [Fact]
    public async Task Reemplazar_la_pregunta_que_sigue_a_un_saludo_funciona()
    {
        var ct = TestContext.Current.CancellationToken;
        var banco = Banco();

        var primero = await banco.Capa().ResponderAsync(
            Secretaria, null, "hola", ct, claveDelCliente: "clave-1");
        Assert.Equal(EstadoDelTurno.Respondida, primero.Estado);
        Assert.Equal(0, banco.Proveedor.Llamadas);

        var segundo = Banco(banco.Hilos, banco.Historial, banco.ValidezDeRetroalimentacion,
            ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.");
        var reemplazo = await segundo.Capa().ResponderAsync(
            Secretaria, primero.Hilo, "¿cuántas materias hay?", ct,
            claveDelCliente: "clave-2", reemplaza: "clave-1");

        Assert.Equal(EstadoDelTurno.Respondida, reemplazo.Estado);
        var fila = Assert.Single(((HistorialEnMemoria)banco.Historial).Turnos);
        Assert.Contains("materias", fila.Pregunta, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reemplazar_la_pregunta_que_sigue_a_una_meta_pregunta_funciona()
    {
        var ct = TestContext.Current.CancellationToken;
        var banco = Banco();

        var primero = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿qué podés hacer?", ct, claveDelCliente: "clave-1");
        Assert.Equal(EstadoDelTurno.Respondida, primero.Estado);

        var segundo = Banco(banco.Hilos, banco.Historial, banco.ValidezDeRetroalimentacion,
            ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.");
        var reemplazo = await segundo.Capa().ResponderAsync(
            Secretaria, primero.Hilo, "¿cuántos docentes están designados?", ct,
            claveDelCliente: "clave-2", reemplaza: "clave-1");

        Assert.Equal(EstadoDelTurno.Respondida, reemplazo.Estado);
        Assert.Single(((HistorialEnMemoria)banco.Historial).Turnos);
    }

    [Fact]
    public async Task Reemplazar_la_pregunta_que_dejo_un_menu_de_aclaracion_abierto_funciona()
    {
        await AgregarColisionesAsync();
        var ct = TestContext.Current.CancellationToken;
        var banco = Banco();

        var menu = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿Quiénes dan Bases de Datos?", ct, claveDelCliente: "clave-1");
        Assert.Equal(EstadoDelTurno.NecesitaAclaracion, menu.Estado);

        var segundo = Banco(banco.Hilos, banco.Historial, banco.ValidezDeRetroalimentacion,
            ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.");
        var reemplazo = await segundo.Capa().ResponderAsync(
            Secretaria, menu.Hilo, "¿cuántos docentes están designados?", ct,
            claveDelCliente: "clave-2", reemplaza: "clave-1");

        Assert.Equal(EstadoDelTurno.Respondida, reemplazo.Estado);
        // El menú quedó reemplazado: no hay una aclaración pendiente colgada.
        Assert.Null(banco.Hilos.Resolver(menu.Hilo, Secretaria).AclaracionPendiente);
        Assert.Single(((HistorialEnMemoria)banco.Historial).Turnos);
    }

    [Fact]
    public async Task Reemplazar_la_pregunta_que_termino_degradada_antes_del_carril_sql_funciona()
    {
        var ct = TestContext.Current.CancellationToken;
        var (basica, pii) = CadenasDeLectura();
        var opciones = new OpcionesAsistente { FallosParaAbrirElBreaker = 1 };
        var banco = BancoDelAsistente.Armar(basica, pii, ClasificadorDeSensibilidad(), Apertura, opciones);
        banco.Breaker.Fallo();

        var primero = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos docentes hay?", ct, claveDelCliente: "clave-1");
        Assert.Equal(EstadoDelTurno.ServicioDegradado, primero.Estado);

        var segundo = Banco(banco.Hilos, banco.Historial, banco.ValidezDeRetroalimentacion,
            ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.");
        var reemplazo = await segundo.Capa().ResponderAsync(
            Secretaria, primero.Hilo, "¿cuántos docentes están designados?", ct,
            claveDelCliente: "clave-2", reemplaza: "clave-1");

        Assert.Equal(EstadoDelTurno.Respondida, reemplazo.Estado);
        Assert.Single(((HistorialEnMemoria)banco.Historial).Turnos);
    }

    [Fact]
    public async Task Reemplazar_el_ultimo_turno_de_una_conversacion_reanudada_cuyo_ultimo_turno_fue_social()
    {
        var ct = TestContext.Current.CancellationToken;
        var hilos = NuevosHilos();
        var idDelTurnoSocial = Guid.NewGuid();

        var sembrado = hilos.Sembrar(
            Secretaria,
            Guid.NewGuid(),
            [new TurnoDelHilo("hola", DateTimeOffset.UtcNow, null, TurnoHistoricoId: idDelTurnoSocial)]);

        var banco = Banco(hilos, ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.");
        var reemplazo = await banco.Capa().ResponderAsync(
            Secretaria, sembrado.Id, "¿cuántos docentes están designados?", ct,
            claveDelCliente: "clave-nueva", reemplaza: idDelTurnoSocial.ToString());

        Assert.Equal(EstadoDelTurno.Respondida, reemplazo.Estado);
    }

    // ------------------------------------------------------------------ apoyo

    private BancoDelAsistente Banco(params string[] guion) =>
        Banco(NuevosHilos(), guion);

    private BancoDelAsistente Banco(IAlmacenDeHilos hilos, params string[] guion)
    {
        var (basica, pii) = CadenasDeLectura();

        return BancoDelAsistente.Armar(
            basica, pii, ClasificadorDeSensibilidad(),
            Apertura, hilos: hilos, guion: guion);
    }

    /// <summary>
    /// Un banco que continúa un hilo EXISTENTE y comparte además su historial y su almacén de
    /// tokens de retroalimentación — los tres, en vez de sólo el hilo, para poder verificar un
    /// reemplazo de punta a punta: «sólo queda la versión final» y «el token viejo se revoca»
    /// necesitan que la segunda llamada escriba sobre el MISMO historial y consulte el MISMO
    /// almacén de tokens que la primera.
    /// </summary>
    private BancoDelAsistente Banco(
        IAlmacenDeHilos hilos,
        IRegistroDeHistorial historial,
        IValidezDeRetroalimentacion validezDeRetroalimentacion,
        params string[] guion)
    {
        var (basica, pii) = CadenasDeLectura();

        return BancoDelAsistente.Armar(
            basica, pii, ClasificadorDeSensibilidad(),
            Apertura,
            hilos: hilos,
            historial: historial,
            validezDeRetroalimentacionCompartida: validezDeRetroalimentacion,
            guion: guion);
    }

    private IAlmacenDeHilos NuevosHilos() =>
        new AlmacenDeHilosEnMemoria(
            Options.Create(new OpcionesAsistente()), TimeProvider.System);

    private async Task AgregarColisionesAsync()
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"""
            INSERT INTO identity.materias (id, code, name, is_active)
            VALUES ('70000000-0000-4000-8000-0000000009f1', '04910', 'Bases de Datos', true);
            INSERT INTO identity.materias_plan (plan_id, materia_id)
            SELECT pl.id, '70000000-0000-4000-8000-0000000009f1' FROM identity.planes pl
             WHERE pl.carrera_id = '{Industrial}' AND pl.vigente AND pl.activo;
            """,
            conexion);

        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

}
