using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica la capa conversacional de punta a punta, contra una base real.
/// </summary>
/// <remarks>
/// El proveedor va guionado, así que lo que se prueba es el <b>cableado</b>: qué
/// pieza corre, en qué orden, cuántas llamadas al modelo cuesta cada camino y qué
/// se le manda. La calidad de la reescritura es lo que mide el eje conversacional
/// de la evaluación, y necesita un proveedor real.
/// </remarks>
[Collection(ColeccionPostgres.Nombre)]
public sealed class CapaConversacionalTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_conversacion")
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
        await SembrarAsync();
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
        await SembrarAsync();
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
        await SembrarAsync();
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
        await SembrarAsync();
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
        // EL TEST DEL CAMBIO DE TEMA. No mira la salida del modelo: mira qué se le
        // mandó. El pivote se fuerza vaciando el historial, así que la reescritura
        // ni siquiera ocurre — y por eso es imposible que arrastre.
        await SembrarAsync();
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
        await SembrarAsync();
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
        await SembrarAsync();
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
        await SembrarAsync();
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
        await SembrarAsync();
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
        await SembrarAsync();
        var banco = Banco(ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.");

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, Guid.NewGuid(), "¿Cuántos docentes están designados?",
            TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.Respondida, turno.Estado);
    }

    [Fact]
    public async Task Un_hilo_ajeno_se_rechaza()
    {
        await SembrarAsync();
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
        await SembrarAsync();
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
        // La misma restricción que el enmascarador impuso en la salida, sostenida
        // en el historial: si el hilo guardara filas, los datos personales
        // volverían al prompt por la puerta del historial.
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;
        var banco = Banco(
            ProveedorGuionado.Generacion(
                "SELECT apellido, documento FROM identity.personas ORDER BY legajo"),
            "Encontré varias personas.");

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿Qué documentos hay cargados?", ct);

        var hilo = banco.Hilos.Resolver(turno.Hilo, Secretaria);
        var todo = string.Join("\n", hilo.Turnos.Select(t => t.Pregunta));

        Assert.DoesNotContain("28341567", todo, StringComparison.Ordinal);
        Assert.DoesNotContain("López", todo, StringComparison.Ordinal);
    }

    // ------------------------------------------- enrutador de dominio, en sombra

    [Fact]
    public async Task Una_pregunta_que_el_carril_determinista_capturaria_responde_igual_que_antes()
    {
        // La propiedad que hace seguro al modo sombra: la decisión se toma y la
        // respuesta no cambia. Mientras no haya a dónde enrutar, cualquier otra cosa
        // sería un carril a medio conectar, que es peor que ninguno.
        await SembrarAsync();
        var banco = Banco(ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.");

        var turno = await banco.Capa().ResponderAsync(
            Secretaria,
            null,
            "¿en qué estado está el pedido de Gómez?",
            TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.Respondida, turno.Estado);
        Assert.Equal(CarrilDelTurno.Sql, ((RegistroEnMemoria)banco.Registro).Turnos.Single().Carril);
    }

    [Fact]
    public async Task El_enrutador_de_dominio_no_agrega_ninguna_llamada_al_modelo()
    {
        // Dos llamadas: generación y redacción. Las mismas que antes de que el
        // enrutador existiera; decidir el carril cuesta cero.
        await SembrarAsync();
        var banco = Banco(ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.");

        var turno = await banco.Capa().ResponderAsync(
            Secretaria,
            null,
            "¿en qué estado está el pedido de Gómez?",
            TestContext.Current.CancellationToken);

        Assert.Equal(2, turno.LlamadasAlModelo);
        Assert.Equal(2, banco.Proveedor.Llamadas);
    }

    [Fact]
    public async Task Un_turno_capturado_que_pide_aclaracion_registra_el_carril_de_aclaracion()
    {
        // Las dos columnas son dos hechos distintos y ninguno implica al otro. Acá se
        // ven separadas: el enrutador captura —«Gómez» resuelve el único slot que la
        // intención exige— y el turno igual termina en el menú, porque la pregunta
        // nombra ADEMÁS una materia que colisiona.
        //
        // `carril` tiene que decir «aclaración», que es lo que pasó de verdad, y no
        // el carril determinista, que no resolvió nada.
        await SembrarAsync();
        await AgregarColisionesAsync();

        var banco = Banco(ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.");

        var turno = await banco.Capa().ResponderAsync(
            Secretaria,
            null,
            "¿en qué estado está el pedido de Gómez de Bases de Datos?",
            TestContext.Current.CancellationToken);

        var fila = ((RegistroEnMemoria)banco.Registro).Turnos.Single();

        Assert.Equal(EstadoDelTurno.NecesitaAclaracion, turno.Estado);
        Assert.Equal(CarrilDelTurno.Aclaracion, fila.Carril);
        Assert.Equal("estado-del-pedido-de-una-persona", fila.IntencionSombra);
    }

    [Fact]
    public async Task La_respuesta_al_usuario_es_la_misma_con_captura_y_sin_ella()
    {
        // LA PROPIEDAD QUE HACE SEGURO AL MODO SOMBRA, probada contra sí misma: la
        // MISMA pregunta, el MISMO guion, y la única diferencia es si el catálogo la
        // capturó o no.
        //
        // Se la hace no capturar vaciando el catálogo del DOMINIO —ningún slot
        // resuelve, ninguna intención queda reconocida— y no colisionando la entidad,
        // que dispararía el detector de ambigüedad y cambiaría el turno entero en vez
        // de cambiar solamente la decisión sombra.
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;
        const string pregunta = "¿en qué estado está el pedido de Gómez?";

        var conCaptura = Banco(ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.");
        var capturado = await conCaptura.Capa().ResponderAsync(Secretaria, null, pregunta, ct);

        var sinCaptura = BancoSinDominio(
            ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.");
        var suelto = await sinCaptura.Capa().ResponderAsync(Secretaria, null, pregunta, ct);

        // Primero, que las dos corridas efectivamente decidieron distinto. Sin esto
        // el test compararía dos veces lo mismo y daría verde sin probar nada.
        Assert.Equal(
            "estado-del-pedido-de-una-persona",
            ((RegistroEnMemoria)conCaptura.Registro).Turnos.Single().IntencionSombra);
        Assert.Null(((RegistroEnMemoria)sinCaptura.Registro).Turnos.Single().IntencionSombra);

        Assert.Equal(capturado.Estado, suelto.Estado);
        Assert.Equal(capturado.Respuesta, suelto.Respuesta);
        Assert.Equal(capturado.Categoria, suelto.Categoria);
        Assert.Equal(capturado.PreguntaInterpretada, suelto.PreguntaInterpretada);
        Assert.Equal(capturado.LlamadasAlModelo, suelto.LlamadasAlModelo);
        Assert.Equal(capturado.Columnas, suelto.Columnas);
        Assert.Equal(capturado.Filas.Count, suelto.Filas.Count);
    }

    [Fact]
    public async Task Una_colision_termina_en_aclaracion_y_no_en_el_carril_determinista()
    {
        // El enrutador corre ANTES del detector de ambigüedad, así que hay que
        // probar que no le roba el turno. La pregunta lo tienta a propósito: dice
        // «plantel», que es el término exacto de una intención del catálogo. Con dos
        // «Bases de Datos» el slot no resuelve, el enrutador no captura, y el menú
        // de aclaración llega igual.
        await SembrarAsync();
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
    // ------------------------------------------------------------------ apoyo

    private BancoDelAsistente Banco(params string[] guion) =>
        Banco(NuevosHilos(), guion);

    private BancoDelAsistente Banco(IAlmacenDeHilos hilos, params string[] guion)
    {
        var (basica, pii) = CadenasDeLectura();

        return BancoDelAsistente.Armar(
            basica, pii, ClasificadorDeSensibilidad(), hilos: hilos, guion: guion);
    }

    private IAlmacenDeHilos NuevosHilos() =>
        new AlmacenDeHilosEnMemoria(
            Options.Create(new OpcionesAsistente()), TimeProvider.System);

    /// <summary>Un banco cuyo enrutador no puede capturar nada.</summary>
    /// <remarks>
    /// El catálogo del dominio vacío deja a todos los slots sin resolver, así que
    /// ninguna intención queda reconocida. Sólo lo consume el resolutor: el detector
    /// de ambigüedad y el de cambio de tema leen el índice de entidades, que sigue
    /// completo, y por eso el resto del turno corre idéntico.
    /// </remarks>
    private BancoDelAsistente BancoSinDominio(params string[] guion)
    {
        var (basica, pii) = CadenasDeLectura();

        return BancoDelAsistente.Armar(
            basica,
            pii,
            ClasificadorDeSensibilidad(),
            hilos: NuevosHilos(),
            dominio: new DominioSinValores(),
            guion: guion);
    }

    private sealed class DominioSinValores : ICatalogoDelDominio
    {
        public Task<CatalogoDelDominio> ObtenerAsync(CancellationToken ct) =>
            Task.FromResult(new CatalogoDelDominio([]));
    }

    private async Task AgregarColisionesAsync()
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"""
            INSERT INTO identity.materias (id, code, name, carrera_id, is_active)
            VALUES ('70000000-0000-4000-8000-0000000009f1', '04910', 'Bases de Datos',
                    '{Industrial}', true);
            """,
            conexion);

        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task SembrarAsync()
    {
        var sql = await File.ReadAllTextAsync(
            Path.Combine(RaizRepositorio.Ruta(), "infra", "scripts", "seed-data", "sintetico.sql"),
            TestContext.Current.CancellationToken);

        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion) { CommandTimeout = 60 };
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
