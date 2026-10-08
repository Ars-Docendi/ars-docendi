using ArsDocendi.IntegrationTests.Infraestructura;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// El buscador de menciones «@materia»/«#docente» (design.md D10 de
/// asistente-rediseno-v3, ARS-148): alcance, sin columnas <c>sensible-*</c>, y el
/// tope de resultados.
/// </summary>
/// <remarks>
/// Igual criterio que <see cref="RlsAlcanceTests"/>: corre con el rol básico de
/// sólo lectura y el actor fijado —nunca la conexión dueña, nunca un filtro de
/// C# como única guardia—, así que lo que se prueba es el límite que impone el
/// motor.
/// </remarks>
public sealed class MencionesTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_menciones")
{
    private static readonly Guid Docente = Guid.Parse("a0000000-0000-4000-8000-000000000001");
    private static readonly Guid Coordinador = Guid.Parse("a0000000-0000-4000-8000-000000000003");
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");

    /// <summary>Ámbito del coordinador en el seed: Ingeniería en Informática.</summary>
    private static readonly Guid CarreraInformatica = Guid.Parse("c0000000-0000-4000-8000-000000000201");

    /// <summary>Una materia de Ingeniería Industrial: fuera del ámbito del coordinador.</summary>
    private static readonly Guid MateriaDeIndustrial = Guid.Parse("70000000-0000-4000-8000-000000000201");

    /// <summary>
    /// La única persona del seed cuya ÚNICA designación cae en una materia de
    /// Industrial: fuera del ámbito del coordinador, sea cual sea el seed del
    /// día (ver el comentario de <c>RlsAlcanceTests</c> sobre por qué los
    /// conteos no se fijan a mano — el id sí se puede fijar: es de fixture,
    /// no un conteo).
    /// </summary>
    private static readonly Guid DocenteDeIndustrial = Guid.Parse("d0000000-0000-4000-8000-000000000010");

    private IBuscadorDeMenciones Buscador() => new BuscadorDeMenciones(Apertura);

    // -------------------------------------------------------------- materias

    [Fact]
    public async Task Un_actor_de_carrera_no_encuentra_materias_de_otra_carrera()
    {
        await SembrarAsync();

        var resultado = await Buscador().BuscarAsync(
            Coordinador, TipoDeMencion.Materia, "organiz", TestContext.Current.CancellationToken);

        Assert.DoesNotContain(resultado.Resultados, r => r.Id == MateriaDeIndustrial);
    }

    [Fact]
    public async Task Un_actor_global_si_encuentra_una_materia_de_otra_carrera()
    {
        await SembrarAsync();

        var resultado = await Buscador().BuscarAsync(
            Secretaria, TipoDeMencion.Materia, "organiz", TestContext.Current.CancellationToken);

        Assert.Contains(resultado.Resultados, r => r.Id == MateriaDeIndustrial);
    }

    [Fact]
    public async Task Cada_resultado_de_materia_trae_carrera_y_codigo()
    {
        await SembrarAsync();

        var resultado = await Buscador().BuscarAsync(
            Secretaria, TipoDeMencion.Materia, "algoritmos", TestContext.Current.CancellationToken);

        var fila = Assert.Single(resultado.Resultados);
        Assert.Equal("Ingeniería en Informática", fila.Carrera);
        Assert.False(string.IsNullOrWhiteSpace(fila.Codigo));
        Assert.Null(fila.Cargo);
    }

    [Fact]
    public async Task Busca_tambien_por_codigo()
    {
        await SembrarAsync();

        // '03620' es el código sembrado de Algoritmos y Estructuras de Datos.
        var resultado = await Buscador().BuscarAsync(
            Secretaria, TipoDeMencion.Materia, "03620", TestContext.Current.CancellationToken);

        Assert.Contains(resultado.Resultados, r => r.Nombre == "Algoritmos y Estructuras de Datos");
    }

    [Fact]
    public async Task Una_palabra_intermedia_del_nombre_matchea()
    {
        await SembrarAsync();

        // "Estructuras" es la segunda palabra de "Algoritmos y Estructuras de
        // Datos": el prefijo por PALABRA tiene que alcanzarla igual que si
        // fuera la primera.
        var resultado = await Buscador().BuscarAsync(
            Secretaria, TipoDeMencion.Materia, "estruct", TestContext.Current.CancellationToken);

        Assert.Contains(resultado.Resultados, r => r.Nombre == "Algoritmos y Estructuras de Datos");
    }

    // --------------------------------------------------------------- docentes

    [Fact]
    public async Task Un_docente_fuera_del_alcance_no_aparece_para_el_coordinador()
    {
        await SembrarAsync();

        var resultado = await Buscador().BuscarAsync(
            Coordinador, TipoDeMencion.Docente, "suárez", TestContext.Current.CancellationToken);

        Assert.DoesNotContain(resultado.Resultados, r => r.Id == DocenteDeIndustrial);
    }

    [Fact]
    public async Task Un_actor_global_si_encuentra_al_docente_de_otra_carrera()
    {
        await SembrarAsync();

        var resultado = await Buscador().BuscarAsync(
            Secretaria, TipoDeMencion.Docente, "suárez", TestContext.Current.CancellationToken);

        Assert.Contains(resultado.Resultados, r => r.Id == DocenteDeIndustrial);
    }

    [Fact]
    public async Task Un_actor_sin_permiso_de_designaciones_no_encuentra_ningun_docente()
    {
        await SembrarAsync();

        // El docente (rol de ámbito de materia) sólo tiene portal.ver/portal.editar
        // — ver 009_designaciones_rls_asistente.sql—, así que la policy le deja
        // la tabla entera vacía, sea cual sea el término.
        var resultado = await Buscador().BuscarAsync(
            Docente, TipoDeMencion.Docente, "a", TestContext.Current.CancellationToken);

        Assert.Empty(resultado.Resultados);
    }

    [Fact]
    public async Task Un_docente_visible_trae_nombre_completo_y_cargo()
    {
        await SembrarAsync();

        var resultado = await Buscador().BuscarAsync(
            Secretaria, TipoDeMencion.Docente, "díaz", TestContext.Current.CancellationToken);

        var fila = Assert.Single(resultado.Resultados);
        Assert.Contains("Marina", fila.Nombre, StringComparison.Ordinal);
        Assert.Contains("Díaz", fila.Nombre, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(fila.Cargo));
        Assert.Null(fila.Carrera);
        Assert.Null(fila.Codigo);
    }

    // -------------------------------------------------------------- resolver

    [Fact]
    public async Task ResolverAsync_encuentra_una_materia_dentro_del_alcance_por_id()
    {
        await SembrarAsync();

        var resuelta = await Buscador().ResolverAsync(
            Secretaria, TipoDeMencion.Materia,
            Guid.Parse("70000000-0000-4000-8000-000000000102"),
            TestContext.Current.CancellationToken);

        Assert.NotNull(resuelta);
        Assert.Equal("Algoritmos y Estructuras de Datos", resuelta!.Nombre);
    }

    [Fact]
    public async Task ResolverAsync_no_encuentra_una_materia_fuera_del_alcance()
    {
        await SembrarAsync();

        var resuelta = await Buscador().ResolverAsync(
            Coordinador, TipoDeMencion.Materia, MateriaDeIndustrial, TestContext.Current.CancellationToken);

        Assert.Null(resuelta);
    }

    [Fact]
    public async Task ResolverAsync_de_un_id_inexistente_y_uno_fuera_de_alcance_se_ven_igual()
    {
        // Sin oráculo de existencia (design.md D11): las dos causas dan `null`.
        await SembrarAsync();

        var inexistente = await Buscador().ResolverAsync(
            Coordinador, TipoDeMencion.Materia, Guid.NewGuid(), TestContext.Current.CancellationToken);
        var fueraDeAlcance = await Buscador().ResolverAsync(
            Coordinador, TipoDeMencion.Materia, MateriaDeIndustrial, TestContext.Current.CancellationToken);

        Assert.Null(inexistente);
        Assert.Null(fueraDeAlcance);
    }

    // ------------------------------------------------------------------ tope

    [Fact]
    public async Task Un_termino_muy_corto_no_es_responsabilidad_de_este_puerto()
    {
        // El rechazo de "menos de 2 caracteres" es del controller (`400` antes de
        // llegar acá, tarea 7.2); este puerto no vuelve a validarlo, así que un
        // término de una letra simplemente busca por prefijo de una letra.
        await SembrarAsync();

        var resultado = await Buscador().BuscarAsync(
            Secretaria, TipoDeMencion.Materia, "a", TestContext.Current.CancellationToken);

        Assert.True(resultado.Resultados.Count <= 6);
    }

    [Fact]
    public async Task Hay_como_maximo_seis_resultados_y_nunca_un_conteo()
    {
        await SembrarAsync();

        // "de" matchea varias materias del seed ("... de Datos", "... de
        // Software", etc.) — alcanza con un término laxo, sin fijar cuántas.
        var resultado = await Buscador().BuscarAsync(
            Secretaria, TipoDeMencion.Materia, "de", TestContext.Current.CancellationToken);

        Assert.True(resultado.Resultados.Count <= 6);
    }

    // ---------------------------------------------------- sensibilidad (D10)

    [Fact]
    public void Ninguna_columna_que_el_buscador_toca_esta_clasificada_sensible()
    {
        // Chequeo ESTÁTICO contra el manifiesto: `identity.materias.name/code`,
        // `identity.carreras.name`, `identity.personas.nombre/apellido` y
        // `designaciones.cargos.nombre` tienen que seguir siendo `publica`. Si
        // alguien reclasificara una de éstas a `sensible-*`, este test cae antes
        // de que el buscador la devuelva.
        var manifiesto = ManifiestoDeSensibilidad.Cargar();

        void AssertPublica(string schema, string tabla, string columna) =>
            Assert.Equal(
                ClasificacionDeSensibilidad.Publica, manifiesto.Clasificacion(schema, tabla, columna));

        AssertPublica("identity", "materias", "name");
        AssertPublica("identity", "materias", "code");
        AssertPublica("identity", "carreras", "name");
        AssertPublica("identity", "personas", "nombre");
        AssertPublica("identity", "personas", "apellido");
        AssertPublica("designaciones", "cargos", "nombre");
    }
}
