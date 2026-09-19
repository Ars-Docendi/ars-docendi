using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica el selector de ejemplos por similitud léxica.
/// </summary>
/// <remarks>
/// Todo corre en memoria y sin red: es exactamente lo que el diseño promete del
/// selector, así que un test que necesitara una conexión estaría probando otra
/// cosa.
///
/// La verificación de que el catálogo es <b>disjunto</b> del dataset de capacidad
/// vive con el dataset, en el cambio de evaluación: no se puede verificar contra
/// un archivo que todavía no existe.
/// </remarks>
public sealed class SelectorDeEjemplosTests
{
    private static readonly SelectorDeEjemplos Selector = new();

    [Fact]
    public void El_catalogo_no_esta_vacio()
    {
        Assert.NotEmpty(Selector.Catalogo);
    }

    [Fact]
    public void Toda_consulta_del_catalogo_pasa_el_validador()
    {
        // Un ejemplo que el propio validador rechazaría le estaría enseñando al
        // modelo a escribir consultas que después se van a rechazar.
        var rechazados = Selector.Catalogo
            .Select(ejemplo => (ejemplo.Pregunta, Veredicto: ValidadorDeSql.Validar(ejemplo.Sql)))
            .Where(par => !par.Veredicto.EsValida)
            .Select(par => $"{par.Pregunta} — {par.Veredicto.Motivo}")
            .ToArray();

        Assert.Empty(rechazados);
    }

    [Fact]
    public void Ningun_ejemplo_tiene_campos_vacios()
    {
        Assert.DoesNotContain(Selector.Catalogo, ejemplo =>
            string.IsNullOrWhiteSpace(ejemplo.Pregunta)
            || string.IsNullOrWhiteSpace(ejemplo.Sql)
            || string.IsNullOrWhiteSpace(ejemplo.Categoria));
    }

    [Fact]
    public void No_hay_preguntas_repetidas_en_el_catalogo()
    {
        var repetidas = Selector.Catalogo
            .GroupBy(e => e.Pregunta, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();

        Assert.Empty(repetidas);
    }

    [Fact]
    public void El_catalogo_cubre_varias_categorias()
    {
        // Un catálogo de una sola categoría le enseñaría al modelo una sola forma
        // de responder, y el dataset de capacidad está estratificado por
        // dificultad justamente porque las formas son distintas.
        var categorias = Selector.Catalogo
            .Select(e => e.Categoria)
            .Distinct(StringComparer.Ordinal)
            .Count();

        Assert.True(categorias >= 3, $"El catálogo cubre solo {categorias} categoría(s).");
    }

    // ------------------------------------------------------------- selección

    [Fact]
    public void Elige_los_ejemplos_de_la_familia_de_la_pregunta()
    {
        var elegidos = Selector.Elegir("¿Qué docentes están designados en Bases de Datos?");

        Assert.NotEmpty(elegidos);
        Assert.Contains(elegidos, e => e.Pregunta.Contains("designados", StringComparison.Ordinal));
    }

    [Fact]
    public void Los_acentos_y_las_mayusculas_no_cambian_la_seleccion()
    {
        var conAcentos = Selector.Elegir("¿Cuántos profesores están designados en la cátedra?");
        var sinAcentos = Selector.Elegir("CUANTOS PROFESORES ESTAN DESIGNADOS EN LA CATEDRA");

        Assert.Equal(
            conAcentos.Select(e => e.Pregunta),
            sinAcentos.Select(e => e.Pregunta));
    }

    [Fact]
    public void Los_sinonimos_del_dominio_llevan_a_la_misma_seleccion()
    {
        // «trámite» y «pedido» nombran lo mismo en el Departamento. Sin unificarlos,
        // dos personas preguntando lo mismo obtendrían ejemplos distintos.
        var conTramite = Selector.Elegir("¿Qué trámites están pendientes de aprobación?");
        var conPedido = Selector.Elegir("¿Qué pedidos están pendientes de aprobación?");

        Assert.Equal(
            conTramite.Select(e => e.Pregunta),
            conPedido.Select(e => e.Pregunta));
    }

    [Fact]
    public void Una_pregunta_sin_parentesco_no_arrastra_ejemplos()
    {
        // Mandarle al modelo los ejemplos menos malos de un catálogo que no viene
        // al caso lo empuja a forzar la pregunta dentro de una forma que no le
        // corresponde.
        var elegidos = Selector.Elegir("¿Cuál es la capital de Francia?");

        Assert.Empty(elegidos);
    }

    [Fact]
    public void Una_pregunta_de_puras_palabras_vacias_no_arrastra_ejemplos()
    {
        var elegidos = Selector.Elegir("¿y esto? ¿de eso, para lo otro?");

        Assert.Empty(elegidos);
    }

    [Fact]
    public void Las_palabras_vacias_no_ganan_contra_un_termino_del_dominio()
    {
        var elegidos = Selector.Elegir("¿Cuántas materias tiene cada carrera?");

        // El primero tiene que ser el que comparte los términos del dominio, no
        // el que comparte «cada» o «tiene».
        Assert.NotEmpty(elegidos);
        Assert.Contains("materias", elegidos[0].Pregunta, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void La_seleccion_tiene_tope()
    {
        // Una pregunta que se parece a casi todo el catálogo igual respeta el tope:
        // cada ejemplo de más es texto que se paga en el prompt variable.
        var elegidos = Selector.Elegir(
            "docente materia carrera pedido designacion cargo periodo rol permiso estado hora");

        Assert.True(elegidos.Count <= 4, $"Se eligieron {elegidos.Count} ejemplos.");
    }

    [Fact]
    public void La_seleccion_es_estable_entre_llamadas()
    {
        var primera = Selector.Elegir("¿Qué pedidos están pendientes?");
        var segunda = Selector.Elegir("¿Qué pedidos están pendientes?");

        Assert.Equal(primera.Select(e => e.Pregunta), segunda.Select(e => e.Pregunta));
    }

    [Fact]
    public void La_seleccion_no_consume_llamadas_al_modelo()
    {
        // La comprobación honesta de «cuesta cero»: con el techo del turno agotado,
        // pedir la selección tiene que seguir funcionando. Si por dentro llamara al
        // proveedor, acá levantaría TechoDeLlamadasSuperado.
        var contador = new ContadorDeLlamadasDelTurno(1);
        contador.Reservar();
        Assert.Throws<TechoDeLlamadasSuperado>(contador.Reservar);

        var elegidos = Selector.Elegir("¿Qué docentes están designados en Bases de Datos?");

        Assert.NotEmpty(elegidos);
    }

    // ----------------------------------------------------------------- huella

    [Fact]
    public void La_huella_es_estable_entre_instancias()
    {
        Assert.Equal(new SelectorDeEjemplos().Huella, new SelectorDeEjemplos().Huella);
    }

    [Fact]
    public void La_huella_tiene_forma_de_resumen_criptografico()
    {
        // SHA-256 en hexadecimal: 64 caracteres. Un GetHashCode() daría algo más
        // corto y, peor, distinto en cada arranque del proceso.
        Assert.Equal(64, Selector.Huella.Length);
        Assert.All(Selector.Huella, caracter => Assert.Contains(caracter, "0123456789abcdef"));
    }

    // --------------------------------------------- inyección en el mensaje

    [Fact]
    public void Los_ejemplos_llegan_al_prompt_de_usuario()
    {
        var elegidos = Selector.Elegir("¿Qué docentes están designados en Bases de Datos?");
        var mensaje = GeneradorDeSql.ArmarMensaje(
            "¿Qué docentes están designados en Bases de Datos?",
            elegidos,
            new DateOnly(2026, 8, 24));

        Assert.NotEmpty(elegidos);
        Assert.Contains(elegidos[0].Pregunta, mensaje, StringComparison.Ordinal);
        Assert.Contains(elegidos[0].Sql, mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public void Sin_ejemplos_el_mensaje_no_deja_una_seccion_vacia()
    {
        var mensaje = GeneradorDeSql.ArmarMensaje(
            "¿Cuál es la capital de Francia?", [], new DateOnly(2026, 8, 24));

        Assert.DoesNotContain("Ejemplos de preguntas", mensaje, StringComparison.Ordinal);
    }
}
