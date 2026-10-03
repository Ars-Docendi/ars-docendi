using ArsDocendi.IntegrationTests.Evaluacion;
using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica las tres piezas puras del seguimiento: el reconocedor de la respuesta a
/// una aclaración, el detector de cambio de tema y el prompt del reescritor.
/// </summary>
/// <remarks>
/// Todo en memoria. El detector de cambio de tema se prueba acá <b>antes</b> que
/// nada contra base, y es deliberado: su riesgo concreto es romper el caso canónico
/// de seguimiento, y eso se ve con un catálogo de tres entradas.
/// </remarks>
public sealed class SeguimientoYAclaracionTests
{
    private static readonly DateTimeOffset Momento = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);

    /// <summary>Catálogo mínimo con lo que hace falta para decidir.</summary>
    private static readonly CatalogoDeEntidades Catalogo = new(
    [
        new ValorDelDominio(ClaseDeEntidad.Materia, "Álgebra", "algebra", "Ingeniería en Informática"),
        new ValorDelDominio(ClaseDeEntidad.Materia, "Sistemas Operativos", "sistemas operativos", "Ingeniería en Informática"),
        new ValorDelDominio(ClaseDeEntidad.Materia, "Sistemas", "sistemas", "Ingeniería en Informática"),
        new ValorDelDominio(ClaseDeEntidad.Persona, "Gómez", "gomez", "Ana Gómez"),
    ]);

    // ================================================== cambio de tema (ARS-43)

    [Fact]
    public void El_seguimiento_canonico_no_marca_cambio_de_tema()
    {
        // EL TEST QUE PROTEGE EL CASO MÁS COMÚN. «¿y en Sistemas?» menciona un
        // término del catálogo que no está activo, así que cumple la segunda
        // condición del detector. La guarda del marcador anafórico es lo único que
        // evita que se lo lea como pivote.
        var historial = Historial("¿Cuántos docentes están designados en Álgebra?");

        Assert.False(DetectorDeCambioDeTema.EsPivote("¿y en Sistemas?", historial, Catalogo));
    }

    [Theory]
    [InlineData("¿y en Sistemas Operativos?")]
    [InlineData("¿y eso cuándo fue?")]
    [InlineData("¿la misma en Sistemas?")]
    [InlineData("¿y ahí cuántos hay?")]
    public void Un_marcador_anaforico_impide_el_pivote(string mensaje)
    {
        var historial = Historial("¿Cuántos docentes están designados en Álgebra?");

        Assert.False(DetectorDeCambioDeTema.EsPivote(mensaje, historial, Catalogo));
    }

    [Fact]
    public void Otra_entidad_sin_anafora_marca_cambio_de_tema()
    {
        var historial = Historial("¿Cuántos docentes están designados en Álgebra?");

        Assert.True(DetectorDeCambioDeTema.EsPivote(
            "¿Qué pedidos tiene Gómez?", historial, Catalogo));
    }

    [Fact]
    public void Sin_historial_no_hay_pivote()
    {
        Assert.False(DetectorDeCambioDeTema.EsPivote("¿Qué pedidos tiene Gómez?", [], Catalogo));
    }

    [Fact]
    public void Una_pregunta_sobre_la_misma_entidad_no_es_pivote()
    {
        var historial = Historial("¿Cuántos docentes están designados en Álgebra?");

        Assert.False(DetectorDeCambioDeTema.EsPivote(
            "¿Qué cargos hay en Álgebra?", historial, Catalogo));
    }

    [Fact]
    public void Una_pregunta_general_sin_entidades_no_es_pivote()
    {
        // No nombra nada del índice. Soltarle el contexto la empeoraría, así que el
        // detector se abstiene: pivotar de más es tan malo como no pivotar.
        var historial = Historial("¿Cuántos docentes están designados en Álgebra?");

        Assert.False(DetectorDeCambioDeTema.EsPivote(
            "¿Cuántos son en total?", historial, Catalogo));
    }

    // ================================================ reconocedor (ARS-41)

    private static readonly Aclaracion Menu = new(
        "Análisis Matemático",
        "¿Quiénes dan Análisis Matemático?",
        [
            new OpcionDeAclaracion("Ingeniería en Informática", "resuelta informatica"),
            new OpcionDeAclaracion("Ingeniería en Electrónica", "resuelta electronica"),
        ]);

    [Fact]
    public void Paso_uno_reconoce_la_etiqueta_completa()
    {
        var reconocida = ReconocedorDeAclaracion.Reconocer("Ingeniería en Informática", Menu);

        Assert.Equal(Reconocimiento.Elegida, reconocida.Estado);
        Assert.Equal("Ingeniería en Informática", reconocida.Opcion!.Etiqueta);
    }

    [Fact]
    public void Paso_dos_reconoce_un_token_distintivo()
    {
        // «informática» distingue; «ingeniería» no distingue nada.
        var reconocida = ReconocedorDeAclaracion.Reconocer("informática", Menu);

        Assert.Equal(Reconocimiento.Elegida, reconocida.Estado);
        Assert.Equal("Ingeniería en Informática", reconocida.Opcion!.Etiqueta);
    }

    [Fact]
    public void Paso_tres_reconoce_el_ordinal()
    {
        var reconocida = ReconocedorDeAclaracion.Reconocer("la 2", Menu);

        Assert.Equal(Reconocimiento.Elegida, reconocida.Estado);
        Assert.Equal("Ingeniería en Electrónica", reconocida.Opcion!.Etiqueta);
    }

    [Fact]
    public void Un_token_compartido_no_se_resuelve_al_azar()
    {
        // «ingeniería» está en las dos: el usuario señaló el grupo, no un miembro.
        // Elegir la primera sería adivinar, que es lo que esta capa evita.
        var reconocida = ReconocedorDeAclaracion.Reconocer("ingeniería", Menu);

        Assert.Equal(Reconocimiento.Ambigua, reconocida.Estado);
        Assert.Null(reconocida.Opcion);
    }

    [Fact]
    public void Nombrar_las_dos_opciones_es_ambiguo()
    {
        var reconocida = ReconocedorDeAclaracion.Reconocer(
            "Ingeniería en Informática o Ingeniería en Electrónica", Menu);

        Assert.Equal(Reconocimiento.Ambigua, reconocida.Estado);
    }

    [Theory]
    [InlineData("no sé")]
    [InlineData("cualquiera")]
    [InlineData("7")]
    public void Una_respuesta_que_no_se_parece_a_nada_no_se_reconoce(string respuesta)
    {
        Assert.Equal(
            Reconocimiento.NoReconocida,
            ReconocedorDeAclaracion.Reconocer(respuesta, Menu).Estado);
    }

    [Fact]
    public void Un_ordinal_fuera_de_rango_no_se_reconoce()
    {
        Assert.Equal(
            Reconocimiento.NoReconocida,
            ReconocedorDeAclaracion.Reconocer("opción 5", Menu).Estado);
    }

    [Fact]
    public void La_opcion_reconocida_trae_la_pregunta_resuelta()
    {
        // Lo que sigue recibe la etiqueta canónica y su pregunta autocontenida, no
        // el texto del usuario.
        var reconocida = ReconocedorDeAclaracion.Reconocer("2", Menu);

        Assert.Equal("resuelta electronica", reconocida.Opcion!.PreguntaResuelta);
    }

    // ================================================== reescritor (ARS-42)

    [Fact]
    public void La_regla_enumera_los_campos_del_dominio()
    {
        // Una regla que dijera «conservá todo lo vigente» produce arrastre
        // silencioso. Los campos van uno por uno.
        Assert.All(
            new[] { "Carrera", "Materia", "Período", "Cargo", "Persona" },
            campo => Assert.Contains(
                campo, ReescritorDePreguntas.Instrucciones, StringComparison.Ordinal));
    }

    [Fact]
    public void El_prompt_trae_un_ejemplo_que_arrastra_y_uno_que_descarta()
    {
        // Sin el de descarte, la única forma demostrada de resolver el prompt es
        // arrastrar, y el modelo arrastra siempre.
        Assert.Contains(
            "EJEMPLO A", ReescritorDePreguntas.Instrucciones, StringComparison.Ordinal);
        Assert.Contains(
            "EJEMPLO B", ReescritorDePreguntas.Instrucciones, StringComparison.Ordinal);
        Assert.Contains(
            "se descarta todo el historial",
            ReescritorDePreguntas.Instrucciones,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Ningun_ejemplo_del_prompt_replica_un_item_del_dataset()
    {
        // Un ejemplo copiado del dataset estaría entrenando contra la métrica.
        var delDataset = DatasetDeCapacidadTests.Cargar().Items
            .Select(item => NormalizadorLexico.Terminos(item.Pregunta))
            .ToArray();

        var lineasDeEjemplo = ReescritorDePreguntas.Instrucciones
            .Split('\n')
            .Where(linea => linea.TrimStart().StartsWith("Anterior:", StringComparison.Ordinal)
                            || linea.TrimStart().StartsWith("Mensaje:", StringComparison.Ordinal)
                            || linea.TrimStart().StartsWith("Reescrita:", StringComparison.Ordinal))
            .Select(linea => linea[(linea.IndexOf(':', StringComparison.Ordinal) + 1)..].Trim())
            .Where(texto => texto.Length > 0)
            .ToArray();

        Assert.NotEmpty(lineasDeEjemplo);

        var colisiones = lineasDeEjemplo
            .Select(texto => (Texto: texto, Terminos: NormalizadorLexico.Terminos(texto)))
            .Where(ejemplo => ejemplo.Terminos.Count > 0)
            .Where(ejemplo => delDataset.Any(item =>
                item.Count > 0
                && (item.SetEquals(ejemplo.Terminos) || ejemplo.Terminos.IsSubsetOf(item))))
            .Select(ejemplo => ejemplo.Texto)
            .ToArray();

        Assert.Empty(colisiones);
    }

    [Fact]
    public void Sin_historial_no_se_arma_prompt_con_turnos()
    {
        var prompt = ReescritorDePreguntas.ArmarMensaje("¿Qué carreras hay?", []);

        Assert.DoesNotContain("Anterior:", prompt, StringComparison.Ordinal);
        Assert.Contains("¿Qué carreras hay?", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Con_historial_el_prompt_lleva_los_turnos_anteriores()
    {
        var prompt = ReescritorDePreguntas.ArmarMensaje(
            "¿y en Sistemas?", Historial("¿Cuántos docentes hay en Álgebra?"));

        Assert.Contains("Anterior: ¿Cuántos docentes hay en Álgebra?", prompt, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Una_reescritura_vacia_conserva_el_original(string respuesta)
    {
        Assert.Equal(
            "¿y en Sistemas?",
            ReescritorDePreguntas.Interpretar(respuesta, "¿y en Sistemas?"));
    }

    [Fact]
    public void Una_reescritura_disparatada_conserva_el_original()
    {
        var parrafo = string.Join(' ', Enumerable.Repeat("palabra", 500));

        Assert.Equal("¿y en Sistemas?", ReescritorDePreguntas.Interpretar(parrafo, "¿y en Sistemas?"));
    }

    [Fact]
    public void Una_reescritura_entre_comillas_se_limpia()
    {
        Assert.Equal(
            "¿Cuántos docentes hay en Sistemas?",
            ReescritorDePreguntas.Interpretar(
                "\"¿Cuántos docentes hay en Sistemas?\"", "¿y en Sistemas?"));
    }

    // ------------------------------------------------------------------ apoyo

    private static IReadOnlyList<TurnoDelHilo> Historial(params string[] preguntas) =>
        [.. preguntas.Select(pregunta => new TurnoDelHilo(pregunta, Momento))];
}
