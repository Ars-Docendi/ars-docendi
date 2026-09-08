using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;

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
        Assert.False(PoliticaDeAbstencion.ConvieneReintentar(Resultado([]), alcanzaTodo: false));
    }

    [Fact]
    public void Con_actor_global_un_vacio_si_gasta_el_reintento()
    {
        // Para un actor global, cero filas sí significa cero filas: el
        // comportamiento no cambia respecto del caso base.
        Assert.True(PoliticaDeAbstencion.ConvieneReintentar(Resultado([]), alcanzaTodo: true));
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
        var texto = PoliticaDeAbstencion.TextoDeResultadoVacio(alcanzaTodo: false);

        // «No hay designaciones registradas» sería falso: la verdad es «no podés
        // verlas».
        Assert.Contains("alcance", texto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exista", texto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void El_vacio_de_un_actor_global_si_puede_decir_que_no_hay()
    {
        var texto = PoliticaDeAbstencion.TextoDeResultadoVacio(alcanzaTodo: true);

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
        var reglas = PoliticaDeAbstencion.ReglasDeRedaccion(alcanzaTodo: false, truncado: false);

        Assert.Single(reglas);
        Assert.Contains("no existe", reglas[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("alcance", reglas[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Con_actor_global_y_sin_truncado_el_prompt_no_agrega_reglas()
    {
        Assert.Empty(PoliticaDeAbstencion.ReglasDeRedaccion(alcanzaTodo: true, truncado: false));
    }

    [Fact]
    public void Con_truncado_el_prompt_prohibe_afirmar_conteos()
    {
        var reglas = PoliticaDeAbstencion.ReglasDeRedaccion(alcanzaTodo: true, truncado: true);

        Assert.Single(reglas);
        Assert.Contains("total", reglas[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cuántos quedaron afuera", reglas[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Los_dos_casos_juntos_agregan_las_dos_reglas()
    {
        Assert.Equal(
            2, PoliticaDeAbstencion.ReglasDeRedaccion(alcanzaTodo: false, truncado: true).Count);
    }

    // ------------------------------------------------ prompt de redacción

    [Fact]
    public void Las_filas_llegan_al_prompt_de_redaccion()
    {
        var mensaje = RedactorDeRespuesta.ArmarMensaje(
            "¿Quiénes dan Bases de Datos?",
            Resultado([["Pérez", "Ana"], ["Gómez", "Luis"]], ["apellido", "nombre"]),
            alcanzaTodo: true);

        Assert.Contains("Pérez | Ana", mensaje, StringComparison.Ordinal);
        Assert.Contains("Gómez | Luis", mensaje, StringComparison.Ordinal);
        Assert.Contains("apellido | nombre", mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public void La_pregunta_llega_al_prompt_de_redaccion()
    {
        var mensaje = RedactorDeRespuesta.ArmarMensaje(
            "¿Quiénes dan Bases de Datos?", Resultado([["Pérez"]]), alcanzaTodo: true);

        Assert.Contains("¿Quiénes dan Bases de Datos?", mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public void El_marco_de_alcance_aparece_en_el_prompt_cuando_el_actor_es_acotado()
    {
        var mensaje = RedactorDeRespuesta.ArmarMensaje(
            "¿Quiénes dan Bases de Datos?", Resultado([["Pérez"]]), alcanzaTodo: false);

        Assert.Contains("alcance", mensaje, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no existe", mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void La_prohibicion_de_conteo_aparece_en_el_prompt_cuando_hay_truncado()
    {
        var mensaje = RedactorDeRespuesta.ArmarMensaje(
            "¿Quiénes dan Bases de Datos?",
            Resultado([["Pérez"]], truncado: true),
            alcanzaTodo: true);

        Assert.Contains("recortó", mensaje, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cuántos quedaron afuera", mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sin_truncado_y_con_actor_global_el_prompt_no_lleva_advertencias()
    {
        var mensaje = RedactorDeRespuesta.ArmarMensaje(
            "¿Quiénes dan Bases de Datos?", Resultado([["Pérez"]]), alcanzaTodo: true);

        Assert.DoesNotContain("IMPORTANTE", mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_resultado_muy_largo_avisa_que_es_una_muestra()
    {
        var filas = Enumerable.Range(0, 60)
            .Select(indice => (IReadOnlyList<object?>)new object?[] { $"fila-{indice}" })
            .ToArray();

        var mensaje = RedactorDeRespuesta.ArmarMensaje(
            "¿Quiénes dan clases?", Resultado(filas), alcanzaTodo: true);

        // El tope del prompt es por costo, no por seguridad. Pero si entran menos
        // filas de las que hay, el modelo tiene que saberlo o narraría un total.
        Assert.Contains("muestra", mensaje, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fila-59", mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public void Los_nulos_se_muestran_como_falta_de_dato()
    {
        var mensaje = RedactorDeRespuesta.ArmarMensaje(
            "¿Qué teléfonos hay?", Resultado([["Pérez", null]]), alcanzaTodo: true);

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

    // ------------------------------- el razonamiento de un turno sin filas

    // EL RAZONAMIENTO SE ESCRIBE ANTES DE EJECUTAR LA CONSULTA, así que sólo puede
    // hablar de lo que el modelo BUSCÓ. Cuando además promete lo que va a devolver
    // —«y devuelvo tres materias distintas»— y la consulta vuelve vacía, el turno
    // afirma dos cosas contradictorias: el texto principal dice que no encontró
    // nada y la explicación dice que devolvió tres. Quien lee se queda con la que
    // suena informada.
    //
    // Pasó de verdad: una consulta cuyo literal decía «Ingeniería Informática»
    // contra una carrera llamada «Ingeniería en Informática» volvió vacía, y la
    // explicación siguió prometiendo tres materias.

    [Fact]
    public void El_razonamiento_de_un_turno_vacio_aclara_que_no_hubo_filas()
    {
        var razonamiento = PoliticaDeAbstencion.RazonamientoDeResultadoVacio(
            "Interpreté 'profesores' como titular, asociado y adjunto, y devuelvo tres materias.");

        Assert.Contains("no devolvió", razonamiento, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("busqué", razonamiento, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void La_aclaracion_conserva_como_se_interpreto_la_pregunta()
    {
        // No se descarta el texto del modelo: es lo único que deja ver que el
        // literal estaba mal escrito, y fue lo que permitió diagnosticar el caso
        // real. Se le quita la promesa, no la información.
        const string Original = "Interpreté 'profesores' como los cargos de jerarquía.";

        Assert.StartsWith(
            Original,
            PoliticaDeAbstencion.RazonamientoDeResultadoVacio(Original),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Sin_razonamiento_no_se_inventa_ninguno(string vacio)
    {
        // Una aclaración sola, sin nada que aclarar, sería ruido: el usuario ya
        // leyó «no encontré ningún registro» dos renglones más arriba.
        Assert.Equal(vacio, PoliticaDeAbstencion.RazonamientoDeResultadoVacio(vacio));
    }

    [Fact]
    public void La_aclaracion_no_habla_de_esquema_ni_de_sql()
    {
        // Misma regla que el resto de los textos de abstención: lo lee el usuario
        // final (D15).
        var razonamiento = PoliticaDeAbstencion.RazonamientoDeResultadoVacio("Algo.");

        Assert.DoesNotContain("SQL", razonamiento, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tabla", razonamiento, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void El_prompt_de_generacion_prohibe_prometer_resultados()
    {
        // El arreglo mecánico de arriba es la garantía; esto es la fuente. Sin
        // esta regla el modelo sigue escribiendo la promesa y la aclaración queda
        // contradiciéndola en cada turno vacío.
        Assert.Contains(
            "todavía no se ejecutó",
            InstruccionesDeGeneracion.Instrucciones,
            StringComparison.Ordinal);
    }

    // ----------------------------- la referencia que la reescritura no resolvió

    // EL CONTRATO QUE NADIE VERIFICABA. El reescritor promete «una pregunta que se
    // entienda sola» y a veces devuelve algo que no se entiende solo: expande la
    // anáfora sin resolverla —«los profesores de esa materia mencionada entre las 3
    // materias»— y el generador se abstiene, que es correcto sobre una pregunta que
    // no se entiende. Lo que estaba mal era el mensaje: «no puedo responder eso con
    // la información que tengo disponible» hace pensar que el dato no existe.

    [Theory]
    [InlineData("¿y los profesores de esa materia?")]
    [InlineData("dame el detalle de ese pedido")]
    [InlineData("¿cuántos hay en la misma carrera?")]
    [InlineData("mostrame los del anterior")]
    public void Un_demostrativo_sin_resolver_se_reconoce(string pregunta)
    {
        Assert.True(PoliticaDeAbstencion.HayReferenciaSinResolver(pregunta));
    }

    [Theory]
    [InlineData("¿los profesores de Ingeniería de Software?")]
    [InlineData("¿cuántos docentes están designados?")]
    [InlineData("dame las materias de la carrera de Informática")]
    public void Una_pregunta_autocontenida_no_se_marca(string pregunta)
    {
        // ESTE ES EL TEST QUE IMPORTA, y el que se rompe si alguien «deduplica» esta
        // lista contra la de DetectorDeCambioDeTema. Aquélla incluye `el`, `los` y
        // `las` a propósito —le sirve para NO pivotar ante cualquier atadura al
        // contexto—, y como detector de referencia sin resolver marcaría casi toda
        // frase en español. Son dos listas parecidas que responden preguntas
        // distintas.
        Assert.False(PoliticaDeAbstencion.HayReferenciaSinResolver(pregunta));
    }

    [Fact]
    public void El_texto_dice_que_no_se_pudo_resolver_la_referencia_y_pide_nombrarla()
    {
        var texto = PoliticaDeAbstencion.TextoReferenciaSinResolver;

        // No afirma que el dato no exista, que es justo lo que decía el genérico.
        Assert.NotEqual(PoliticaDeAbstencion.TextoNoContestable, texto);
        Assert.Contains("referís", texto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void El_texto_de_la_referencia_tampoco_habla_de_esquema_ni_de_sql()
    {
        var texto = PoliticaDeAbstencion.TextoReferenciaSinResolver;

        Assert.DoesNotContain("consulta", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tabla", texto, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------- el alcance por dominio

    // EL DEFECTO QUE ESTO CIERRA, medido contra el padrón sintético. El actor de
    // Decanato es global y tiene `designaciones.ver`, así que el booleano del turno
    // daba verdadero; la RLS de portal le devolvía un solo perfil —el suyo— y a
    // «¿qué docentes saben Python?» —que tres personas declararon— el asistente
    // contestaba «no encontré ningún registro». Es la violación de BR-asistente-003
    // sobre el dominio que esa regla no cubría.
    //
    // El propio ConsultorDeAlcance lo había anticipado: «es UN permiso porque hoy
    // hay UN dominio con policies. Cuando haya un segundo —portal es el candidato
    // inmediato— esto deja de ser un booleano».

    [Fact]
    public void Sin_el_permiso_de_portal_una_consulta_de_portal_no_alcanza_todo()
    {
        var perfil = new PerfilDelActor(
            EsGlobal: true, VeDatosPersonales: true,
            AlcanzaDesignaciones: true, VeTrayectoriaAjena: false);

        Assert.False(PoliticaDeAbstencion.AlcanzaTodo(perfil, laConsultaTocaPortal: true));
    }

    [Fact]
    public void Con_el_permiso_de_portal_una_consulta_de_portal_si_alcanza_todo()
    {
        var perfil = new PerfilDelActor(
            EsGlobal: true, VeDatosPersonales: true,
            AlcanzaDesignaciones: true, VeTrayectoriaAjena: true);

        Assert.True(PoliticaDeAbstencion.AlcanzaTodo(perfil, laConsultaTocaPortal: true));
    }

    [Fact]
    public void Una_consulta_que_no_toca_portal_conserva_su_evaluacion()
    {
        // La mitad que no se puede romper al arreglar la otra: el permiso de portal
        // no puede pasar a hacer falta para preguntas que no son de portal.
        var perfil = new PerfilDelActor(
            EsGlobal: true, VeDatosPersonales: true,
            AlcanzaDesignaciones: true, VeTrayectoriaAjena: false);

        Assert.True(PoliticaDeAbstencion.AlcanzaTodo(perfil, laConsultaTocaPortal: false));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Un_actor_no_global_nunca_alcanza_todo(bool tocaPortal)
    {
        var perfil = new PerfilDelActor(
            EsGlobal: false, VeDatosPersonales: false,
            AlcanzaDesignaciones: false, VeTrayectoriaAjena: true);

        Assert.False(PoliticaDeAbstencion.AlcanzaTodo(perfil, tocaPortal));
    }

    // ------------------------------------------------------------------ apoyo

    private static ResultadoDeConsulta Resultado(
        IReadOnlyList<IReadOnlyList<object?>> filas,
        IReadOnlyList<string>? columnas = null,
        bool truncado = false) =>
        new(columnas ?? ["columna"], filas, truncado);
}
