using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica la política de abstención y la redacción (RF-17, §3.3).
/// </summary>
/// <remarks>
/// En memoria: la política es un conjunto de funciones puras sobre un resultado
/// ya obtenido, y el prompt de redacción se arma sin llamar a nadie. Verificar las
/// reglas de abstención a través de la respuesta del modelo probaría al modelo, no
/// a este código.
/// </remarks>
public sealed class AbstencionYRedaccionTests
{
    // ------------------------------------------------------ guard de vacío

    [Fact]
    public void Cero_filas_es_vacio()
    {
        Assert.True(Resultado([]).EstaVacio);
    }

    [Fact]
    public void Una_fila_de_nulos_es_vacio()
    {
        // Una agregación sobre cero filas devuelve UNA fila con nulos, no cero
        // filas. Un guard que solo mirara el conteo la daría por resultado con
        // datos y la redacción hablaría de un máximo que no existe.
        Assert.True(Resultado([[null, null]]).EstaVacio);
    }

    [Fact]
    public void Una_fila_con_un_cero_no_es_vacio()
    {
        // count(*) sobre nada devuelve cero, y cero SÍ dice algo.
        Assert.False(Resultado([[0L]]).EstaVacio);
    }

    [Fact]
    public void Una_fila_con_algun_valor_no_nulo_no_es_vacio()
    {
        Assert.False(Resultado([[null, "Pérez", null]]).EstaVacio);
    }

    [Fact]
    public void Varias_filas_de_nulos_no_son_vacio()
    {
        // Dos filas de nulos no vienen de una agregación vacía: vienen de un
        // conjunto con dos elementos cuyos valores son nulos, que es un dato.
        Assert.False(Resultado([[null], [null]]).EstaVacio);
    }

    // -------------------------------------------------------- el reintento

    [Fact]
    public void Con_actor_acotado_un_vacio_no_gasta_el_reintento()
    {
        // RLS convierte «no tenés permiso» en cero filas, que es la MISMA firma
        // que «el literal no matcheó». Reintentar acá gasta el único reintento en
        // un caso donde ningún reintento puede ayudar.
        Assert.False(PoliticaDeAbstencion.ConvieneReintentar(Resultado([]), actorEsGlobal: false));
    }

    [Fact]
    public void Con_actor_global_un_vacio_si_gasta_el_reintento()
    {
        // Para un actor global, cero filas sí significa cero filas: el
        // comportamiento no cambia respecto del caso base.
        Assert.True(PoliticaDeAbstencion.ConvieneReintentar(Resultado([]), actorEsGlobal: true));
    }

    [Fact]
    public void Un_resultado_con_datos_nunca_gasta_el_reintento()
    {
        Assert.False(PoliticaDeAbstencion.ConvieneReintentar(Resultado([["algo"]]), true));
        Assert.False(PoliticaDeAbstencion.ConvieneReintentar(Resultado([["algo"]]), false));
    }

    [Fact]
    public void Con_actor_acotado_una_agregacion_vacia_tampoco_gasta_el_reintento()
    {
        Assert.False(PoliticaDeAbstencion.ConvieneReintentar(Resultado([[null]]), false));
    }

    // ------------------------------------------------------- los textos

    [Fact]
    public void El_vacio_de_un_actor_acotado_no_afirma_inexistencia()
    {
        var texto = PoliticaDeAbstencion.TextoDeResultadoVacio(actorEsGlobal: false);

        // «No hay designaciones registradas» sería falso: la verdad es «no podés
        // verlas».
        Assert.Contains("alcance", texto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exista", texto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void El_vacio_de_un_actor_global_si_puede_decir_que_no_hay()
    {
        var texto = PoliticaDeAbstencion.TextoDeResultadoVacio(actorEsGlobal: true);

        Assert.NotEqual(PoliticaDeAbstencion.TextoDeResultadoVacio(false), texto);
        Assert.DoesNotContain("alcance", texto, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("tabla")]
    [InlineData("columna")]
    [InlineData("schema")]
    [InlineData("esquema")]
    [InlineData("sql")]
    [InlineData("select")]
    [InlineData("identity")]
    [InlineData("designaciones.")]
    public void Ningun_texto_de_abstencion_habla_de_esquema_ni_de_sql(string prohibida)
    {
        // La razón de un rechazo la lee el usuario final. Un mensaje que diga «no
        // existe la columna personas.salario» le confirma qué columnas SÍ existen:
        // es enumeración por mensaje de error.
        string[] textos =
        [
            PoliticaDeAbstencion.TextoNoContestable,
            PoliticaDeAbstencion.TextoRechazadaPorValidador,
            PoliticaDeAbstencion.TextoServicioDegradado,
            PoliticaDeAbstencion.TextoDeResultadoVacio(true),
            PoliticaDeAbstencion.TextoDeResultadoVacio(false),
        ];

        Assert.All(textos, texto =>
            Assert.DoesNotContain(prohibida, texto, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Ningun_texto_de_abstencion_declara_cuantas_filas_quedaron_afuera()
    {
        // «Ves 3 de 124» es un canal de inferencia sobre datos que el usuario no
        // puede ver: repetido con distintas preguntas permite reconstruirlos.
        string[] textos =
        [
            PoliticaDeAbstencion.TextoDeResultadoVacio(true),
            PoliticaDeAbstencion.TextoDeResultadoVacio(false),
        ];

        Assert.All(textos, texto => Assert.DoesNotContain(texto, c => char.IsDigit(c)));
    }

    // ------------------------------------------- reglas del prompt

    [Fact]
    public void Con_actor_acotado_el_prompt_prohibe_afirmar_inexistencia()
    {
        var reglas = PoliticaDeAbstencion.ReglasDeRedaccion(actorEsGlobal: false, truncado: false);

        Assert.Single(reglas);
        Assert.Contains("no existe", reglas[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("alcance", reglas[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Con_actor_global_y_sin_truncado_el_prompt_no_agrega_reglas()
    {
        Assert.Empty(PoliticaDeAbstencion.ReglasDeRedaccion(actorEsGlobal: true, truncado: false));
    }

    [Fact]
    public void Con_truncado_el_prompt_prohibe_afirmar_conteos()
    {
        var reglas = PoliticaDeAbstencion.ReglasDeRedaccion(actorEsGlobal: true, truncado: true);

        Assert.Single(reglas);
        Assert.Contains("total", reglas[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cuántos quedaron afuera", reglas[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Los_dos_casos_juntos_agregan_las_dos_reglas()
    {
        Assert.Equal(
            2, PoliticaDeAbstencion.ReglasDeRedaccion(actorEsGlobal: false, truncado: true).Count);
    }

    // ------------------------------------------------ prompt de redacción

    [Fact]
    public void Las_filas_llegan_al_prompt_de_redaccion()
    {
        var mensaje = RedactorDeRespuesta.ArmarMensaje(
            "¿Quiénes dan Bases de Datos?",
            Resultado([["Pérez", "Ana"], ["Gómez", "Luis"]], ["apellido", "nombre"]),
            actorEsGlobal: true);

        Assert.Contains("Pérez | Ana", mensaje, StringComparison.Ordinal);
        Assert.Contains("Gómez | Luis", mensaje, StringComparison.Ordinal);
        Assert.Contains("apellido | nombre", mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public void La_pregunta_llega_al_prompt_de_redaccion()
    {
        var mensaje = RedactorDeRespuesta.ArmarMensaje(
            "¿Quiénes dan Bases de Datos?", Resultado([["Pérez"]]), actorEsGlobal: true);

        Assert.Contains("¿Quiénes dan Bases de Datos?", mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public void El_marco_de_alcance_aparece_en_el_prompt_cuando_el_actor_es_acotado()
    {
        var mensaje = RedactorDeRespuesta.ArmarMensaje(
            "¿Quiénes dan Bases de Datos?", Resultado([["Pérez"]]), actorEsGlobal: false);

        Assert.Contains("alcance", mensaje, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no existe", mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void La_prohibicion_de_conteo_aparece_en_el_prompt_cuando_hay_truncado()
    {
        var mensaje = RedactorDeRespuesta.ArmarMensaje(
            "¿Quiénes dan Bases de Datos?",
            Resultado([["Pérez"]], truncado: true),
            actorEsGlobal: true);

        Assert.Contains("recortó", mensaje, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cuántos quedaron afuera", mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sin_truncado_y_con_actor_global_el_prompt_no_lleva_advertencias()
    {
        var mensaje = RedactorDeRespuesta.ArmarMensaje(
            "¿Quiénes dan Bases de Datos?", Resultado([["Pérez"]]), actorEsGlobal: true);

        Assert.DoesNotContain("IMPORTANTE", mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_resultado_muy_largo_avisa_que_es_una_muestra()
    {
        var filas = Enumerable.Range(0, 60)
            .Select(indice => (IReadOnlyList<object?>)new object?[] { $"fila-{indice}" })
            .ToArray();

        var mensaje = RedactorDeRespuesta.ArmarMensaje(
            "¿Quiénes dan clases?", Resultado(filas), actorEsGlobal: true);

        // El tope del prompt es por costo, no por seguridad. Pero si entran menos
        // filas de las que hay, el modelo tiene que saberlo o narraría un total.
        Assert.Contains("muestra", mensaje, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fila-59", mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public void Los_nulos_se_muestran_como_falta_de_dato()
    {
        var mensaje = RedactorDeRespuesta.ArmarMensaje(
            "¿Qué teléfonos hay?", Resultado([["Pérez", null]]), actorEsGlobal: true);

        // Un nulo renderizado como cadena vacía se lee como «el valor es vacío»,
        // que no es lo mismo que «no hay valor».
        Assert.Contains("(sin dato)", mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public void Las_instrucciones_de_redaccion_prohiben_inventar_valores()
    {
        // Una consulta correcta narrada mal es tan falsa como una consulta
        // incorrecta, y el dataset de capacidad compara conjuntos de resultados:
        // no ve la redacción.
        Assert.Contains(
            "No agregues datos", RedactorDeRespuesta.Instrucciones, StringComparison.Ordinal);
        Assert.Contains(
            "No menciones nombres de tablas", RedactorDeRespuesta.Instrucciones, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ apoyo

    private static ResultadoDeConsulta Resultado(
        IReadOnlyList<IReadOnlyList<object?>> filas,
        IReadOnlyList<string>? columnas = null,
        bool truncado = false) =>
        new(columnas ?? ["columna"], filas, truncado);
}
