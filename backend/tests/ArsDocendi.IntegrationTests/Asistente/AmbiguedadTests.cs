using ArsDocendi.Evaluacion.Nucleo.Fixture;
using ArsDocendi.IntegrationTests.Infraestructura;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica el índice de entidades contra una base real y el detector de ambigüedad
/// sobre él.
/// </summary>
/// <remarks>
/// <b>El seed sintético del proyecto no tiene ninguna colisión</b>: sus seis
/// materias tienen nombres distintos y ningún apellido se repite. Por eso los tests
/// que ejercitan la desambiguación insertan las colisiones que necesitan, y esa
/// inserción es también la prueba de que el índice sale de la base: si estuviera
/// embebido en el código, filas nuevas no cambiarían nada.
/// </remarks>
[Collection(ColeccionPostgres.Nombre)]
public sealed class AmbiguedadTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_ambiguedad")
{
    private const string Informatica = "c0000000-0000-4000-8000-000000000201";
    private const string Industrial = "c0000000-0000-4000-8000-000000000202";

    // ------------------------------------------------------------ el índice

    [Fact]
    public async Task El_indice_carga_las_materias_y_las_personas_de_la_base()
    {
        await SembrarAsync();

        var catalogo = await Indice().ObtenerAsync(TestContext.Current.CancellationToken);

        Assert.Contains("bases de datos", catalogo.Terminos);
        Assert.Contains("lopez", catalogo.Terminos);
    }

    [Fact]
    public async Task El_indice_recoge_una_colision_que_no_existia_al_arrancar()
    {
        // Prueba de que sale de la base y no del código: la colisión se crea con un
        // INSERT y el índice la ve.
        await SembrarAsync();
        await AgregarColisionesAsync();

        var catalogo = await Indice().ObtenerAsync(TestContext.Current.CancellationToken);

        Assert.True(catalogo.Colisiona("bases de datos"));
        Assert.Equal(2, catalogo.Valores("bases de datos").Count);
    }

    [Fact]
    public async Task El_indice_se_lee_una_sola_vez()
    {
        await SembrarAsync();
        var indice = Indice();
        var ct = TestContext.Current.CancellationToken;

        await indice.ObtenerAsync(ct);
        await indice.ObtenerAsync(ct);

        Assert.Equal(1, indice.Lecturas);
    }

    // ------------------------------------------------------------ el detector

    [Fact]
    public async Task Una_materia_repetida_entre_carreras_devuelve_las_carreras()
    {
        await SembrarAsync();
        await AgregarColisionesAsync();
        var catalogo = await Indice().ObtenerAsync(TestContext.Current.CancellationToken);

        var aclaracion = DetectorDeAmbiguedad.Detectar(
            "¿Quiénes dan Bases de Datos?", catalogo);

        Assert.NotNull(aclaracion);
        Assert.Equal(2, aclaracion.Opciones.Count);
        Assert.Contains(aclaracion.Opciones, opcion => opcion.Etiqueta.Contains("Informática", StringComparison.Ordinal));
        Assert.Contains(aclaracion.Opciones, opcion => opcion.Etiqueta.Contains("Industrial", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Un_apellido_compartido_devuelve_las_personas_con_nombre_completo()
    {
        await SembrarAsync();
        await AgregarColisionesAsync();
        var catalogo = await Indice().ObtenerAsync(TestContext.Current.CancellationToken);

        var aclaracion = DetectorDeAmbiguedad.Detectar("¿Qué pedidos tiene López?", catalogo);

        Assert.NotNull(aclaracion);
        Assert.All(
            aclaracion.Opciones,
            opcion => Assert.Contains("López", opcion.Etiqueta, StringComparison.Ordinal));
        Assert.Contains(aclaracion.Opciones, opcion => opcion.Etiqueta.StartsWith("Carla", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Con_el_discriminador_presente_no_se_pide_aclaracion()
    {
        // Preguntar sería pedirle al usuario que repita lo que acaba de decir.
        await SembrarAsync();
        await AgregarColisionesAsync();
        var catalogo = await Indice().ObtenerAsync(TestContext.Current.CancellationToken);

        Assert.Null(DetectorDeAmbiguedad.Detectar(
            "¿Quiénes dan Bases de Datos en Ingeniería en Informática?", catalogo));
    }

    [Fact]
    public async Task Una_materia_sin_colision_no_dispara()
    {
        await SembrarAsync();
        var catalogo = await Indice().ObtenerAsync(TestContext.Current.CancellationToken);

        Assert.Null(DetectorDeAmbiguedad.Detectar("¿Quiénes dan Bases de Datos?", catalogo));
    }

    [Theory]
    [InlineData("¿Cuántos docentes hay?")]
    [InlineData("¿Qué pasó con los pedidos?")]
    [InlineData("¿Cuál es el período actual?")]
    public async Task Una_pregunta_vaga_sin_colision_no_dispara(string pregunta)
    {
        // El detector NO se extiende a la vaguedad: preguntar tiene un costo medido
        // y las aclaraciones de calidad baja son peores que no preguntar.
        await SembrarAsync();
        await AgregarColisionesAsync();
        var catalogo = await Indice().ObtenerAsync(TestContext.Current.CancellationToken);

        Assert.Null(DetectorDeAmbiguedad.Detectar(pregunta, catalogo));
    }

    [Fact]
    public async Task La_opcion_trae_una_pregunta_autocontenida()
    {
        await SembrarAsync();
        await AgregarColisionesAsync();
        var catalogo = await Indice().ObtenerAsync(TestContext.Current.CancellationToken);

        var aclaracion = DetectorDeAmbiguedad.Detectar("¿Quiénes dan Bases de Datos?", catalogo);

        Assert.All(
            aclaracion!.Opciones,
            opcion =>
            {
                Assert.Contains("Bases de Datos", opcion.PreguntaResuelta, StringComparison.Ordinal);
                Assert.Contains(opcion.Etiqueta, opcion.PreguntaResuelta, StringComparison.Ordinal);
            });
    }

    // -------------------------------------------------- el fixture del eval

    [Fact]
    public void El_fixture_del_evaluador_declara_las_colisiones_que_el_detector_necesita()
    {
        // Sin ellas, los ítems de diálogo que prueban la desambiguación darían
        // verde sin medir nada: el detector nunca dispararía.
        Assert.Contains(
            GeneradorDeFixture.MateriasCompartidas, compartida => compartida.Carreras > 1);

        Assert.Contains(
            GeneradorDeFixture.ApellidosCompartidos, compartido => compartido.Personas > 1);
    }

    // ------------------------------------------------------------------ apoyo

    private IndiceDeEntidades Indice() => new(CadenasDeLectura().Basica);

    /// <summary>
    /// Agrega una materia con nombre repetido en otra carrera y una persona que
    /// comparte apellido.
    /// </summary>
    /// <remarks>
    /// El seed sintético no trae colisiones, así que sin esto el detector no
    /// tendría nada que detectar y los tests pasarían sin ejercitar el camino.
    /// </remarks>
    private async Task AgregarColisionesAsync()
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"""
            INSERT INTO identity.materias (id, code, name, carrera_id, is_active)
            VALUES ('70000000-0000-4000-8000-0000000009f1', '04910', 'Bases de Datos',
                    '{Industrial}', true);

            INSERT INTO identity.personas (id, documento, cuil, legajo, nombre, apellido)
            VALUES ('d0000000-0000-4000-8000-0000000009f1', '35111222', '20-35111222-9',
                    '9901', 'Damián', 'López');
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
