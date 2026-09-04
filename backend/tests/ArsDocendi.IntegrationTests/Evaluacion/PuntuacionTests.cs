using ArsDocendi.Evaluacion.Nucleo.Puntuacion;
using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Evaluacion;

/// <summary>
/// Verifica la puntuación con penalización y el comparador de resultados.
/// </summary>
/// <remarks>
/// Es la métrica primaria del proyecto hecha número, y todo acá es aritmética pura
/// sobre datos ya obtenidos: no hace falta base ni proveedor. Ese es justamente el
/// motivo por el que estas piezas viven en el núcleo, que sí está en la solución.
/// </remarks>
public sealed class PuntuacionTests
{
    // ------------------------------------------------------ los desenlaces

    [Fact]
    public void Una_traduccion_correcta_suma_uno()
    {
        Assert.Equal(1m, PuntuacionConPenalizacion.Puntuar(DesenlaceDeItem.TraduccionCorrecta, 1m));
    }

    [Fact]
    public void Una_abstencion_correcta_suma_uno()
    {
        Assert.Equal(1m, PuntuacionConPenalizacion.Puntuar(DesenlaceDeItem.AbstencionCorrecta, 1m));
    }

    [Fact]
    public void Abstenerse_ante_algo_contestable_no_suma_ni_resta()
    {
        // Es una falta de capacidad, no una mentira. La métrica castiga afirmar
        // algo falso, no callarse.
        Assert.Equal(
            0m, PuntuacionConPenalizacion.Puntuar(DesenlaceDeItem.AbstencionSobreloFactible, 2m));
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public void Una_traduccion_incorrecta_resta_la_penalizacion(decimal penalizacion)
    {
        Assert.Equal(
            -penalizacion,
            PuntuacionConPenalizacion.Puntuar(DesenlaceDeItem.TraduccionIncorrecta, penalizacion));
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public void Intentar_responder_lo_infactible_resta_la_penalizacion(decimal penalizacion)
    {
        Assert.Equal(
            -penalizacion,
            PuntuacionConPenalizacion.Puntuar(DesenlaceDeItem.IntentoSobreLoInfactible, penalizacion));
    }

    [Fact]
    public void Un_fallo_no_acredita_ni_castiga()
    {
        // No es una respuesta del modelo: acreditarlo inflaría el número y
        // castigarlo culparía al modelo de que se cayó la red.
        Assert.Equal(0m, PuntuacionConPenalizacion.Puntuar(DesenlaceDeItem.Fallo, 2m));
    }

    [Fact]
    public void Una_generacion_truncada_no_acredita_ni_castiga()
    {
        // Resolvió «no contestable» sin que el modelo decidiera nada: es un
        // presupuesto corto, no una abstención. Acreditarla inflaría justo la
        // métrica primaria; castigarla culparía al modelo de un techo que no eligió.
        Assert.Equal(
            0m, PuntuacionConPenalizacion.Puntuar(DesenlaceDeItem.GeneracionTruncada, 2m));
    }

    // --------------------------------------------------- la trampa del eje

    [Fact]
    public void Una_abstencion_con_error_no_se_cuenta_como_abstencion()
    {
        // ÉSTA ES LA TRAMPA. Sin crédito de API, todos los ítems infactibles
        // devuelven «no contestable» —porque el turno falló— y un scoring que solo
        // mirara el booleano los daría por acertados. El eje de abstención, que es
        // la métrica primaria, sería el que MÁS se infla cuando el sistema no
        // funciona.
        var conFallo = Resultados(
            (DesenlaceDeItem.Fallo, 8));

        var puntaje = PuntuacionConPenalizacion.Puntuar(conFallo, 1m);
        var conteos = PuntuacionConPenalizacion.Conteos(conFallo);

        Assert.Equal(0m, puntaje.Puntaje);
        Assert.Equal(0, conteos[DesenlaceDeItem.AbstencionCorrecta]);
    }

    [Fact]
    public void Con_todos_los_turnos_caidos_el_eje_no_muestra_aciertos()
    {
        var corrida = Resultados((DesenlaceDeItem.Fallo, 24));

        var puntaje = PuntuacionConPenalizacion.Puntuar(corrida, 1m);

        // El denominador cuenta TODOS los ítems, también los que fallaron: si no,
        // una corrida con la mitad de los turnos caídos mostraría un normalizado
        // alto sobre un denominador chico, que es justamente el número que engaña.
        Assert.Equal(0m, puntaje.Puntaje);
        Assert.Equal(24m, puntaje.PuntajeMaximo);
        Assert.Equal(0m, puntaje.Normalizado);
    }

    // ------------------------------------------------- las tres penalizaciones

    [Fact]
    public void Se_reporta_con_tres_penalizaciones()
    {
        Assert.Equal(3, PuntuacionConPenalizacion.Penalizaciones.Count);
        Assert.Equal(
            [0.5m, 1.0m, 2.0m],
            PuntuacionConPenalizacion.Penalizaciones);
    }

    [Fact]
    public void Las_tres_penalizaciones_cambian_el_puntaje_y_no_los_conteos()
    {
        var corrida = Resultados(
            (DesenlaceDeItem.TraduccionCorrecta, 6),
            (DesenlaceDeItem.TraduccionIncorrecta, 2),
            (DesenlaceDeItem.AbstencionCorrecta, 3),
            (DesenlaceDeItem.IntentoSobreLoInfactible, 1));

        var puntajes = PuntuacionConPenalizacion.Puntuar(corrida);
        var conteos = PuntuacionConPenalizacion.Conteos(corrida);

        // 9 aciertos, 3 errores. Con penalización 0,5 da 7,5; con 1,0 da 6; con
        // 2,0 da 3. Cuánto castiga mentir es una decisión de producto, y por eso
        // el reporte muestra las tres en vez de esconder la elección.
        Assert.Equal([7.5m, 6.0m, 3.0m], puntajes.Select(p => p.Puntaje));
        Assert.All(puntajes, puntaje => Assert.Equal(12m, puntaje.PuntajeMaximo));

        Assert.Equal(6, conteos[DesenlaceDeItem.TraduccionCorrecta]);
        Assert.Equal(2, conteos[DesenlaceDeItem.TraduccionIncorrecta]);
        Assert.Equal(3, conteos[DesenlaceDeItem.AbstencionCorrecta]);
        Assert.Equal(1, conteos[DesenlaceDeItem.IntentoSobreLoInfactible]);
    }

    [Fact]
    public void Una_corrida_perfecta_normaliza_a_uno()
    {
        var corrida = Resultados(
            (DesenlaceDeItem.TraduccionCorrecta, 7),
            (DesenlaceDeItem.AbstencionCorrecta, 3));

        Assert.All(
            PuntuacionConPenalizacion.Puntuar(corrida),
            puntaje => Assert.Equal(1m, puntaje.Normalizado));
    }

    [Fact]
    public void Mentir_mucho_puede_dar_negativo()
    {
        // Es deseable: un sistema que afirma más falsedades que verdades tiene que
        // dar peor que uno que no responde nada.
        var corrida = Resultados(
            (DesenlaceDeItem.TraduccionCorrecta, 2),
            (DesenlaceDeItem.TraduccionIncorrecta, 8));

        Assert.True(PuntuacionConPenalizacion.Puntuar(corrida, 2m).Puntaje < 0);
    }

    [Fact]
    public void Los_conteos_cubren_todos_los_desenlaces()
    {
        // Un desenlace ausente del diccionario haría fallar el reporte al leerlo.
        var conteos = PuntuacionConPenalizacion.Conteos([]);

        Assert.Equal(Enum.GetValues<DesenlaceDeItem>().Length, conteos.Count);
        Assert.All(conteos.Values, cuantos => Assert.Equal(0, cuantos));
    }

    // ------------------------------------------------------- el comparador

    [Fact]
    public void Dos_consultas_distintas_con_el_mismo_resultado_coinciden()
    {
        var respuesta = Resultado([["Gómez", "Ana"], ["Pérez", "Luis"]], ["apellido", "nombre"]);
        var referencia = Resultado([["Gómez", "Ana"], ["Pérez", "Luis"]], ["apellido", "nombre"]);

        Assert.True(ComparadorDeResultados.Coinciden(respuesta, referencia, ordenImporta: false));
    }

    [Fact]
    public void Un_alias_distinto_no_es_un_error()
    {
        // Los nombres de columna no se comparan: un alias distinto no es un error
        // de traducción.
        var respuesta = Resultado([["Gómez"]], ["ape"]);
        var referencia = Resultado([["Gómez"]], ["apellido"]);

        Assert.True(ComparadorDeResultados.Coinciden(respuesta, referencia, ordenImporta: false));
    }

    [Fact]
    public void El_orden_se_ignora_por_omision()
    {
        var respuesta = Resultado([["b"], ["a"]]);
        var referencia = Resultado([["a"], ["b"]]);

        Assert.True(ComparadorDeResultados.Coinciden(respuesta, referencia, ordenImporta: false));
    }

    [Fact]
    public void El_orden_importa_cuando_el_item_lo_declara()
    {
        var respuesta = Resultado([["b"], ["a"]]);
        var referencia = Resultado([["a"], ["b"]]);

        // «Los tres cargos de mayor a menor» sí lo declara; «qué carreras hay», no.
        Assert.False(ComparadorDeResultados.Coinciden(respuesta, referencia, ordenImporta: true));
    }

    [Fact]
    public void Un_conteo_distinto_de_filas_no_coincide()
    {
        Assert.False(ComparadorDeResultados.Coinciden(
            Resultado([["a"]]), Resultado([["a"], ["b"]]), ordenImporta: false));
    }

    [Fact]
    public void Una_fila_repetida_no_es_lo_mismo_que_una_sola()
    {
        // Multiconjunto y no conjunto: un DISTINCT de más es un error de traducción.
        Assert.False(ComparadorDeResultados.Coinciden(
            Resultado([["a"], ["a"]]), Resultado([["a"], ["b"]]), ordenImporta: false));
    }

    [Fact]
    public void Los_tipos_numericos_distintos_con_el_mismo_valor_coinciden()
    {
        // El tipo que devuelve el motor depende de cómo se escribió la agregación,
        // no de si la respuesta es correcta.
        var respuesta = Resultado([[(long)5]]);
        var referencia = Resultado([[5]]);

        Assert.True(ComparadorDeResultados.Coinciden(respuesta, referencia, ordenImporta: false));
    }

    [Fact]
    public void Un_nulo_no_coincide_con_una_cadena_vacia()
    {
        // «No hay valor» y «el valor es vacío» son cosas distintas, y el asistente
        // no puede confundirlas sin cambiar lo que la respuesta afirma.
        Assert.False(ComparadorDeResultados.Coinciden(
            Resultado([[null]]), Resultado([[""]]), ordenImporta: false));
    }

    [Fact]
    public void Un_separador_dentro_del_valor_no_confunde_dos_filas_distintas()
    {
        // Con un separador imprimible, ("a|b", "c") y ("a", "b|c") producirían el
        // mismo texto y contarían como iguales.
        Assert.False(ComparadorDeResultados.Coinciden(
            Resultado([["a|b", "c"]]), Resultado([["a", "b|c"]]), ordenImporta: false));
    }

    // ------------------------------------------------------------------ apoyo

    private static IReadOnlyList<ResultadoDeItem> Resultados(
        params (DesenlaceDeItem Desenlace, int Cuantos)[] grupos) =>
    [
        .. grupos.SelectMany((grupo, indiceDeGrupo) => Enumerable
            .Range(0, grupo.Cuantos)
            .Select(indice => new ResultadoDeItem(
                $"item-{indiceDeGrupo}-{indice}", "consulta_simple", grupo.Desenlace, "sintético"))),
    ];

    private static ResultadoDeConsulta Resultado(
        IReadOnlyList<IReadOnlyList<object?>> filas, IReadOnlyList<string>? columnas = null) =>
        new(columnas ?? ["columna"], filas, Truncado: false);
}
