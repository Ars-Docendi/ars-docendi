using ArsDocendi.IntegrationTests.Infraestructura;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica el catálogo del dominio y la resolución de slots contra una base real.
/// </summary>
/// <remarks>
/// Va contra base porque lo que se prueba es justamente que los valores salgan de
/// ahí: el vocabulario del trámite se deriva de las restricciones que lo declaran y
/// los cargos de su tabla. Un test con listas escritas a mano probaría las listas.
///
/// <b>El seed sintético no trae colisiones de apellido</b>, así que los casos que
/// ejercitan «no resuelve» insertan la suya. Esa inserción es también la prueba de
/// que el catálogo sale de la base: si estuviera embebido, la fila nueva no
/// cambiaría nada.
/// </remarks>
[Collection(ColeccionPostgres.Nombre)]
public sealed class ResolucionDeSlotsTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_slots")
{
    /// <summary>
    /// Las intenciones que este archivo cubre.
    /// </summary>
    /// <remarks>
    /// La lee <see cref="CatalogoDeIntencionesTests"/> para fallar si el catálogo
    /// crece sin que crezca la cobertura. Se declara a mano a propósito: derivarla
    /// del catálogo haría que el control se cumpliera solo.
    /// </remarks>
    internal static readonly string[] IntencionesConCaso =
    [
        "estado-del-pedido-de-una-persona",
        "pedidos-en-un-estado",
        "pedidos-de-una-novedad",
        "plantel-de-una-materia",
        "designaciones-de-un-cargo",
    ];

    private const string Industrial = "c0000000-0000-4000-8000-000000000202";

    // ------------------------------------------- el vocabulario sale de la base

    [Fact]
    public async Task Los_estados_son_exactamente_los_que_declara_la_restriccion()
    {
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;

        var catalogo = await Catalogo().ObtenerAsync(ct);

        // Los ocho del CHECK de designaciones.pedidos, ni uno más.
        Assert.NotNull(catalogo.Unico(ClaseDeSlot.Estado, "borrador"));
        Assert.NotNull(catalogo.Unico(ClaseDeSlot.Estado, "rechazado"));
        Assert.NotNull(catalogo.Unico(ClaseDeSlot.Estado, "en revision coordinador"));
        Assert.Null(catalogo.Unico(ClaseDeSlot.Estado, "inventado"));
    }

    [Fact]
    public async Task Las_novedades_y_los_tipos_de_baja_salen_de_sus_restricciones()
    {
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;

        var catalogo = await Catalogo().ObtenerAsync(ct);

        Assert.Equal("Alta", catalogo.Unico(ClaseDeSlot.Novedad, "alta")?.Valor);
        Assert.Equal("Renuncia", catalogo.Unico(ClaseDeSlot.TipoDeBaja, "renuncia")?.Valor);

        // «Jubilación» pierde el acento al normalizar, como la pregunta del usuario.
        Assert.Equal("Jubilación", catalogo.Unico(ClaseDeSlot.TipoDeBaja, "jubilacion")?.Valor);
    }

    [Fact]
    public async Task Los_cargos_resuelven_por_nombre_y_por_abreviatura()
    {
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;

        var catalogo = await Catalogo().ObtenerAsync(ct);

        // Las dos formas van al mismo cargo: la gente pregunta «los JTP» y «los jefes
        // de trabajos prácticos», y las dos son la misma pregunta.
        Assert.Equal(
            "Jefe de Trabajos Prácticos",
            catalogo.Unico(ClaseDeSlot.Cargo, "jefe de trabajos practicos")?.Valor);
        Assert.Equal(
            "Jefe de Trabajos Prácticos", catalogo.Unico(ClaseDeSlot.Cargo, "jtp")?.Valor);
    }

    [Fact]
    public async Task Una_restriccion_que_no_enumera_literales_falla_nombrandola()
    {
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;

        await using var conexion = await AbrirConexionAsync();
        await using (var ddl = new NpgsqlCommand(
            """
            ALTER TABLE designaciones.pedidos
              ADD CONSTRAINT pedidos_prueba_sin_literales CHECK (horas IS NULL OR horas >= 0)
            """, conexion))
        {
            await ddl.ExecuteNonQueryAsync(ct);
        }

        var error = await Assert.ThrowsAsync<VocabularioIlegible>(() =>
            LectorDeVocabulario.VocabulariosAsync(
                conexion, "designaciones", "pedidos", ["pedidos_prueba_sin_literales"], ct));

        // Nombrarla es la mitad del punto. La otra mitad es no devolver una lista
        // vacía: un vocabulario vacío no rompe nada y deja el carril barato apagado
        // sin que nadie se entere.
        Assert.Contains("pedidos_prueba_sin_literales", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Una_restriccion_que_no_existe_tambien_falla()
    {
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;

        await using var conexion = await AbrirConexionAsync();

        var error = await Assert.ThrowsAsync<VocabularioIlegible>(() =>
            LectorDeVocabulario.VocabulariosAsync(
                conexion, "designaciones", "pedidos", ["pedidos_renombrada_ayer"], ct));

        Assert.Contains("pedidos_renombrada_ayer", error.Message, StringComparison.Ordinal);
    }

    // --------------------------------------------- el índice queda como estaba

    [Fact]
    public async Task El_vocabulario_del_tramite_no_entra_en_el_indice_de_entidades()
    {
        // La razón de componer afuera en vez de ampliar adentro. El detector de
        // ambigüedad dispara por colisión de términos del índice y el de cambio de
        // tema mide solapamiento de entidades: meterles «borrador» y «Titular»
        // adentro les cambiaría el comportamiento sin que nadie lo pidiera.
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;

        var entidades = await Indice().ObtenerAsync(ct);

        Assert.DoesNotContain("borrador", entidades.Terminos);
        Assert.DoesNotContain("alta", entidades.Terminos);
        Assert.DoesNotContain("jtp", entidades.Terminos);
    }

    [Fact]
    public async Task Dos_turnos_leen_la_base_una_sola_vez()
    {
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;
        var catalogo = Catalogo();

        await catalogo.ObtenerAsync(ct);
        var despuesDelPrimero = catalogo.Lecturas;
        await catalogo.ObtenerAsync(ct);

        Assert.Equal(1, despuesDelPrimero);
        Assert.Equal(1, catalogo.Lecturas);
    }

    // --------------------------------------------- reconocimiento y resolución

    [Fact]
    public async Task Reordenar_las_palabras_reconoce_la_misma_intencion()
    {
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;
        var resolutor = Resolutor();

        var derecha = await resolutor.ResolverAsync("¿en qué estado está el pedido de López?", ct);
        var alReves = await resolutor.ResolverAsync("¿el pedido de López en qué estado está?", ct);

        Assert.Equal("estado-del-pedido-de-una-persona", derecha?.Intencion.Nombre);
        Assert.Equal(derecha?.Intencion.Nombre, alReves?.Intencion.Nombre);
        Assert.Equal("López", alReves?.Slots["persona"].Valor);
    }

    [Fact]
    public async Task Falta_un_termino_y_no_hay_intencion()
    {
        await SembrarAsync();

        // Sin «estado» ni «pedido» no queda ninguna intención con todos sus términos.
        var resuelta = await Resolutor().ResolverAsync(
            "¿qué sabés de López?", TestContext.Current.CancellationToken);

        Assert.Null(resuelta);
    }

    [Fact]
    public async Task Un_apellido_que_no_esta_en_la_base_no_resuelve()
    {
        await SembrarAsync();

        var resuelta = await Resolutor().ResolverAsync(
            "¿en qué estado está el pedido de Wittgenstein?",
            TestContext.Current.CancellationToken);

        Assert.Null(resuelta);
    }

    [Fact]
    public async Task Dos_personas_con_el_mismo_apellido_no_resuelven_el_slot()
    {
        await SembrarAsync();
        await AgregarColisionAsync();

        // Con dos López, enrutar con uno devuelve las filas del otro, y para quien
        // preguntó esa respuesta es indistinguible de la correcta. No resolver manda
        // la pregunta al carril que sí puede responderla o pedir la aclaración.
        var resuelta = await Resolutor().ResolverAsync(
            "¿en qué estado está el pedido de López?", TestContext.Current.CancellationToken);

        Assert.Null(resuelta);
    }

    [Fact]
    public async Task Un_apellido_unico_si_resuelve()
    {
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;

        var resuelta = await Resolutor().ResolverAsync(
            "¿en qué estado está el pedido de Gómez?", ct);

        Assert.Equal("Gómez", resuelta?.Slots["persona"].Valor);
        Assert.Equal("designaciones/pedidos-por-persona", resuelta?.Destino);
    }

    [Fact]
    public async Task Un_estado_del_tramite_resuelve_su_intencion()
    {
        await SembrarAsync();

        var resuelta = await Resolutor().ResolverAsync(
            "¿cuántos pedidos hay en borrador?", TestContext.Current.CancellationToken);

        Assert.Equal("pedidos-en-un-estado", resuelta?.Intencion.Nombre);
        Assert.Equal("borrador", resuelta?.Slots["estado"].Valor);
    }

    [Fact]
    public async Task Una_novedad_resuelve_su_intencion()
    {
        await SembrarAsync();

        var resuelta = await Resolutor().ResolverAsync(
            "¿qué pedidos de Alta hay?", TestContext.Current.CancellationToken);

        Assert.Equal("pedidos-de-una-novedad", resuelta?.Intencion.Nombre);
        Assert.Equal("Alta", resuelta?.Slots["novedad"].Valor);
    }

    [Fact]
    public async Task Una_materia_resuelve_el_plantel()
    {
        await SembrarAsync();

        var resuelta = await Resolutor().ResolverAsync(
            "¿cómo está el plantel de Bases de Datos?", TestContext.Current.CancellationToken);

        Assert.Equal("plantel-de-una-materia", resuelta?.Intencion.Nombre);
        Assert.Equal("Bases de Datos", resuelta?.Slots["materia"].Valor);
    }

    [Fact]
    public async Task Un_cargo_resuelve_su_intencion()
    {
        await SembrarAsync();

        var resuelta = await Resolutor().ResolverAsync(
            "¿cuántos JTP hay?", TestContext.Current.CancellationToken);

        Assert.Equal("designaciones-de-un-cargo", resuelta?.Intencion.Nombre);
        Assert.Equal("Jefe de Trabajos Prácticos", resuelta?.Slots["cargo"].Valor);
    }

    [Fact]
    public async Task Una_materia_que_colisiona_no_resuelve_el_plantel()
    {
        await SembrarAsync();
        await AgregarColisionAsync();

        var resuelta = await Resolutor().ResolverAsync(
            "¿cómo está el plantel de Bases de Datos?", TestContext.Current.CancellationToken);

        Assert.Null(resuelta);
    }

    [Fact]
    public void Ni_el_resolutor_ni_el_catalogo_pueden_llamar_al_modelo()
    {
        // Se verifica sobre las DEPENDENCIAS y no contando llamadas. Un contador en
        // cero solo dice que esta vez no llamó; que el tipo no reciba por dónde
        // llamar dice que no puede, y sigue diciéndolo cuando alguien agregue una
        // rama nueva.
        Type[] piezas = [typeof(ResolutorDeIntenciones), typeof(CatalogoDeIntenciones)];

        foreach (var pieza in piezas)
        {
            var parametros = pieza
                .GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Select(p => p.ParameterType);

            Assert.DoesNotContain(typeof(IProveedorDeModelo), parametros);
        }
    }

    // ------------------------------------------------------------------ apoyo

    private IndiceDeEntidades Indice() => new(CadenasDeLectura().Basica);

    private CatalogoDelDominioReal Catalogo() => new(Indice(), CadenasDeLectura().Basica);

    private ResolutorDeIntenciones Resolutor() =>
        new(CatalogoDeIntenciones.Cargar(), Catalogo());

    /// <summary>Un segundo López y una segunda «Bases de Datos» en otra carrera.</summary>
    private async Task AgregarColisionAsync()
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"""
            INSERT INTO identity.personas (id, documento, cuil, legajo, nombre, apellido)
            VALUES ('d0000000-0000-4000-8000-0000000009f2', '35111333', '20-35111333-9',
                    '9902', 'Damián', 'López');

            INSERT INTO identity.materias (id, code, name, carrera_id, is_active)
            VALUES ('70000000-0000-4000-8000-0000000009f2', '04911', 'Bases de Datos',
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
