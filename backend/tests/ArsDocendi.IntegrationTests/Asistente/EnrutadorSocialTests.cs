using System.Text.Json;
using ArsDocendi.IntegrationTests.Evaluacion;
using ArsDocendi.IntegrationTests.Infraestructura;
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
    // Regresión de un bug encontrado a mano, no por un test. La lista de frases
    // exactas cubría «qué podés hacer» pero no esta forma, así que la primera
    // pregunta que escribió un usuario real —un saludo más una circunlocución en
    // tuteo— se fue al carril SQL, costó una llamada al modelo y terminó en «no
    // puedo responder eso», que es lo contrario de lo que la clase meta existe
    // para dar.
    [InlineData("Hola, que es lo que puedes realizar?")]
    [InlineData("¿qué es lo que podés hacer?")]
    [InlineData("¿qué cosas sabés hacer?")]
    [InlineData("¿qué puedo hacer con vos?")]
    [InlineData("¿me ayudás?")]
    [InlineData("¿qué tipo de preguntas puedo hacerte?")]
    [InlineData("¿para qué me servís?")]
    [InlineData("¿qué información me podés dar?")]
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
    public void El_saludo_y_el_agradecimiento_tienen_respuesta_fija(IntencionSocial intencion)
    {
        // Estas dos sí: son cortesía, no información. Un texto fijo no puede
        // desactualizarse respecto de nada.
        Assert.False(string.IsNullOrWhiteSpace(EnrutadorSocial.Responder(intencion)));
    }

    [Fact]
    public void La_meta_pregunta_NO_tiene_respuesta_fija()
    {
        // Tenía una, y era el problema: un párrafo escrito a mano enumerando qué
        // podía consultar el asistente. Eso es una promesa sobre capacidades que
        // nadie verifica, y se desactualiza en silencio con cada GRANT — decía
        // «designaciones, docentes, materias, pedidos y períodos» sin que nada
        // comprobara que el rol de quien preguntaba pudiera leer esas cinco cosas.
        //
        // Ahora la responde el catálogo de capacidades, derivado de los privilegios
        // efectivos del actor. Que acá levante excepción es la garantía de que nadie
        // pueda volver a escribir el párrafo sin darse cuenta.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EnrutadorSocial.Responder(IntencionSocial.Meta));
    }

    [Fact]
    public void La_meta_pregunta_se_sigue_clasificando_como_meta()
    {
        // Que no tenga texto fijo no significa que deje de detectarse: el enrutador
        // la sigue capturando, y quien la resuelve es la capa con el catálogo.
        Assert.Equal(IntencionSocial.Meta, EnrutadorSocial.Clasificar("¿qué podés hacer?"));
    }

    [Fact]
    public void Pedir_la_respuesta_de_una_intencion_que_no_es_social_falla()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EnrutadorSocial.Responder(IntencionSocial.Ninguna));
    }

    [Fact]
    public void Ninguna_pregunta_de_los_datasets_de_datos_se_clasifica_como_social()
    {
        // EL GUARD QUE PAGA EL CAMBIO DE MECANISMO. Pasar de una lista de frases a
        // un vocabulario hace al enrutador más generoso, y lo generoso se paga
        // capturando preguntas que no le tocan: interceptar «¿qué carreras hay?»
        // con un texto sobre capacidades es peor que no tener la clase meta.
        //
        // Las preguntas salen de los datasets de capacidad y robustez, no de una
        // lista escrita a mano: uno escribe los casos que ya sabe que fallan. Las
        // escribió otra tarea con otro objetivo, así que son legítimas y ajenas.
        var capturadas = new List<string>();

        foreach (var (id, pregunta) in PreguntasDeDatos())
        {
            var intencion = EnrutadorSocial.Clasificar(pregunta);

            if (intencion != IntencionSocial.Ninguna)
            {
                capturadas.Add($"{id} «{pregunta}» quedó como {intencion}");
            }
        }

        Assert.True(capturadas.Count == 0,
            "El enrutador social capturó preguntas de datos. Es vocabulario "
            + "demasiado laxo, no un dataset mal escrito:\n" + string.Join("\n", capturadas));
    }

    private static IEnumerable<(string Id, string Pregunta)> PreguntasDeDatos()
    {
        foreach (var archivo in (string[])["capacidad.json", "robustez.json"])
        {
            var ruta = Path.Combine(
                RaizRepositorio.Ruta(), "backend", "eval", "datasets", archivo);

            using var documento = JsonDocument.Parse(File.ReadAllText(ruta));

            foreach (var item in documento.RootElement.GetProperty("items").EnumerateArray())
            {
                yield return (
                    item.GetProperty("id").GetString()!,
                    item.GetProperty("pregunta").GetString()!);
            }
        }
    }
}
