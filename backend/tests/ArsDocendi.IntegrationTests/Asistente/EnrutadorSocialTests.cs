using ArsDocendi.IntegrationTests.Evaluacion;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica el carril sin datos: qué se intercepta y, sobre todo, qué no.
/// </summary>
/// <remarks>
/// Todo en memoria. La clase es pura a propósito: el eje de evaluación social
/// necesita poder afirmar que un saludo costó cero tokens, y eso solo se sostiene
/// si la clasificación no llama a nada.
///
/// El riesgo real de esta pieza no es dejar pasar un saludo: es comerse una
/// pregunta. Por eso hay más tests de lo que NO se intercepta.
/// </remarks>
public sealed class EnrutadorSocialTests
{
    // -------------------------------------------------------- lo que se atrapa

    [Theory]
    [InlineData("hola")]
    [InlineData("Hola!")]
    [InlineData("buenas")]
    [InlineData("buen día")]
    [InlineData("buenos días")]
    [InlineData("¿qué tal?")]
    [InlineData("hola, ¿cómo andás?")]
    [InlineData("holis")]
    public void Un_saludo_solo_se_clasifica_como_saludo(string mensaje)
    {
        Assert.Equal(IntencionSocial.Saludo, EnrutadorSocial.Clasificar(mensaje));
    }

    [Theory]
    [InlineData("gracias")]
    [InlineData("¡muchas gracias!")]
    [InlineData("mil gracias")]
    [InlineData("dale, gracias")]
    [InlineData("perfecto")]
    [InlineData("listo, gracias")]
    [InlineData("chau")]
    [InlineData("nada más por ahora, gracias")]
    public void Un_cierre_se_clasifica_como_agradecimiento(string mensaje)
    {
        Assert.Equal(IntencionSocial.Agradecimiento, EnrutadorSocial.Clasificar(mensaje));
    }

    [Theory]
    [InlineData("¿qué podés hacer?")]
    [InlineData("¿para qué servís?")]
    [InlineData("¿en qué me podés ayudar?")]
    [InlineData("¿cómo funcionás?")]
    [InlineData("¿qué puedo preguntarte?")]
    [InlineData("ayuda")]
    [InlineData("¿quién sos?")]
    public void Una_pregunta_sobre_el_asistente_es_meta(string mensaje)
    {
        Assert.Equal(IntencionSocial.Meta, EnrutadorSocial.Clasificar(mensaje));
    }

    // ------------------------------------------------------ lo que NO se atrapa

    [Fact]
    public void Un_saludo_con_pregunta_no_se_intercepta()
    {
        // EL CASO QUE JUSTIFICA LA GUARDA. Decidir por la presencia del saludo en
        // vez de por la ausencia de contenido rompe una forma perfectamente normal
        // de preguntar.
        Assert.Equal(
            IntencionSocial.Ninguna,
            EnrutadorSocial.Clasificar("hola, ¿cuántos docentes tiene Inglés Nivel IV?"));
    }

    [Theory]
    [InlineData("gracias, y ¿cuántos pedidos hay?")]
    [InlineData("buenas, necesito los docentes de Álgebra")]
    [InlineData("hola! ¿qué materias tiene Sistemas?")]
    public void Una_pregunta_con_apertura_cortes_no_se_intercepta(string mensaje)
    {
        Assert.Equal(IntencionSocial.Ninguna, EnrutadorSocial.Clasificar(mensaje));
    }

    [Theory]
    [InlineData("¿qué carreras hay?")]
    [InlineData("¿qué materias hay?")]
    [InlineData("¿cómo se llama el docente del legajo 0421?")]
    [InlineData("¿qué pedidos están pendientes?")]
    [InlineData("¿quién es el titular de Bases de Datos?")]
    public void Una_pregunta_de_dominio_no_es_meta(string mensaje)
    {
        // «¿qué carreras hay?» es el caso que el ticket nombra: devolverle al
        // usuario un texto sobre capacidades cuando pidió la lista de carreras es
        // peor que no tener la clase meta.
        Assert.Equal(IntencionSocial.Ninguna, EnrutadorSocial.Clasificar(mensaje));
    }

    [Fact]
    public void Ningun_item_del_dataset_de_capacidad_se_intercepta()
    {
        // El criterio de aceptación medible hoy: activar el enrutador no puede
        // mover un solo ítem del dataset. Si se comiera uno, la métrica primaria
        // del proyecto cambiaría por una tabla de palabras.
        var interceptados = DatasetDeCapacidadTests.Cargar().Items
            .Select(item => (item.Id, item.Pregunta, Intencion: EnrutadorSocial.Clasificar(item.Pregunta)))
            .Where(par => par.Intencion != IntencionSocial.Ninguna)
            .Select(par => $"{par.Id}: «{par.Pregunta}» → {par.Intencion}")
            .ToArray();

        Assert.Empty(interceptados);
    }

    [Fact]
    public void Ninguna_pregunta_del_catalogo_de_ejemplos_se_intercepta()
    {
        // Mismo argumento sobre el otro corpus de preguntas verificadas del
        // proyecto: son las que el selector le muestra al modelo como ejemplos de
        // preguntas legítimas.
        var interceptadas = new SelectorDeEjemplos().Catalogo
            .Where(ejemplo => EnrutadorSocial.Clasificar(ejemplo.Pregunta) != IntencionSocial.Ninguna)
            .Select(ejemplo => ejemplo.Pregunta)
            .ToArray();

        Assert.Empty(interceptadas);
    }

    [Fact]
    public void El_test_del_dataset_no_es_vacio()
    {
        // Anti-vacuidad: si el dataset llegara vacío, los dos tests de arriba
        // pasarían sin clasificar nada.
        Assert.NotEmpty(DatasetDeCapacidadTests.Cargar().Items);
        Assert.NotEmpty(new SelectorDeEjemplos().Catalogo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("?")]
    public void Un_mensaje_sin_palabras_no_se_clasifica(string mensaje)
    {
        Assert.Equal(IntencionSocial.Ninguna, EnrutadorSocial.Clasificar(mensaje));
    }

    // ------------------------------------------------------------ respuestas

    [Theory]
    [InlineData(IntencionSocial.Saludo)]
    [InlineData(IntencionSocial.Agradecimiento)]
    [InlineData(IntencionSocial.Meta)]
    public void Las_tres_clases_tienen_respuesta_fija(IntencionSocial intencion)
    {
        Assert.False(string.IsNullOrWhiteSpace(EnrutadorSocial.Responder(intencion)));
    }

    [Fact]
    public void La_respuesta_meta_no_promete_lo_que_el_sistema_no_hace()
    {
        var texto = EnrutadorSocial.Responder(IntencionSocial.Meta);

        // Solo consulta. Prometer acciones sería una promesa falsa en el único
        // turno donde el usuario está preguntando exactamente qué puede esperar.
        Assert.Contains("no modifico", texto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Pedir_la_respuesta_de_una_intencion_que_no_es_social_falla()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EnrutadorSocial.Responder(IntencionSocial.Ninguna));
    }
}
