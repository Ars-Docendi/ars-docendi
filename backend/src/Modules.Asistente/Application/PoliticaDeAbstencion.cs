namespace Modules.Asistente.Application;

/// <summary>
/// Los siete casos de abstención (RF-17, §3.3 de la definición).
/// </summary>
/// <remarks>
/// Es la métrica primaria del proyecto hecha código: corrección con abstención
/// significa que el asistente nunca afirma algo falso.
///
/// Todo lo de acá son funciones puras sobre datos ya obtenidos. La política no
/// consulta nada ni llama a nadie: recibe el resultado y el alcance del actor, y
/// decide. Eso la hace testeable sin base y sin proveedor, que es lo que se
/// quiere de la pieza que decide cuándo callarse.
/// </remarks>
public static class PoliticaDeAbstencion
{
    /// <summary>
    /// Si para este actor, <b>en esta consulta</b>, cero filas significa que no hay
    /// filas.
    /// </summary>
    /// <remarks>
    /// <b>SE DECIDE POR TURNO Y NO POR ACTOR, y ése es el arreglo.</b> Era un
    /// booleano del perfil —ámbito global y <c>designaciones.ver</c>— y funcionó
    /// mientras hubo un solo dominio con policies. Con portal empezó a mentir: un
    /// actor global con el permiso del trámite y sin el de portal lo tenía en
    /// verdadero, y a «¿qué docentes saben Python?» —que tres personas declararon—
    /// contestaba «no encontré ningún registro». Afirmación falsa, dicha con
    /// seguridad, sobre personas reales.
    ///
    /// El propio <c>ConsultorDeAlcance</c> lo había anotado: «es UN permiso porque
    /// hoy hay UN dominio con policies. Cuando haya un segundo —portal es el
    /// candidato inmediato— esto deja de ser un booleano y pasa a depender de qué
    /// tablas tocó la consulta». Portal llegó y el booleano no se movió.
    ///
    /// <b>Portal SUMA una condición, no reemplaza ninguna.</b> Una consulta que no
    /// toca portal se evalúa exactamente como antes: el permiso de portal no puede
    /// pasar a hacer falta para preguntas que no son de portal.
    ///
    /// <b>Es conservador por construcción.</b> Equivocarse hacia «no alcanzás a
    /// verlo» degrada la respuesta; hacia «no hay» la vuelve falsa. Por eso las
    /// condiciones se conjugan y ninguna se asume.
    /// </remarks>
    /// <param name="perfil">Los ingredientes del alcance, resueltos una vez por turno.</param>
    /// <param name="laConsultaTocaPortal">
    /// Si la consulta generada lee alguna tabla de portal. Lo decide
    /// <see cref="CoberturaDelPortal.TablasQueToca"/>, que es la <b>misma</b>
    /// detección que alimenta la declaración de cobertura: dos detectores del mismo
    /// hecho es cómo una respuesta declara cobertura de portal y a la vez afirma que
    /// no hay datos.
    /// </param>
    public static bool AlcanzaTodo(PerfilDelActor perfil, bool laConsultaTocaPortal)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        return perfil.AlcanzaDesignaciones
            && (!laConsultaTocaPortal || perfil.VeTrayectoriaAjena);
    }

    /// <summary>
    /// Si un resultado vacío justifica gastar el reintento de generación.
    /// </summary>
    /// <remarks>
    /// <b>Éste es el caso central de toda la política.</b> RLS convierte «no tenés
    /// permiso» en cero filas, que es exactamente la misma firma que «el literal
    /// no matcheó»: mismo conteo, mismo tipo de resultado, ninguna señal que los
    /// distinga.
    ///
    /// Confundirlos cuesta dos veces. Gasta el único reintento en un caso donde
    /// ningún reintento puede ayudar —la consulta estaba bien, el alcance no la
    /// alcanza— y hace que la redacción diga «no hay designaciones registradas»
    /// cuando la verdad es «no podés verlas».
    ///
    /// Para un actor global, en cambio, cero filas sí significa cero filas, y el
    /// reintento se comporta como en el caso base.
    /// </remarks>
    public static bool ConvieneReintentar(ResultadoDeConsulta resultado, bool alcanzaTodo)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        return resultado.EstaVacio && alcanzaTodo;
    }

    /// <summary>
    /// Texto de un resultado vacío.
    /// </summary>
    /// <remarks>
    /// <b>Se resuelve sin llamar al modelo.</b> Con cero filas no hay nada que
    /// narrar, así que la segunda llamada no aportaría información y sí podría
    /// inventarla. Resolverlo acá hace que la distinción entre «no hay» y «no
    /// podés verlo» sea mecánica en lugar de depender de que el modelo respete
    /// una instrucción del prompt — que es la diferencia entre una garantía y un
    /// pedido.
    /// </remarks>
    public static string TextoDeResultadoVacio(
        bool alcanzaTodo, CoberturaDeUnDato? cobertura = null)
    {
        var texto = alcanzaTodo
            ? "No encontré ningún registro que responda esa pregunta."
            : "No encontré nada dentro de lo que podés consultar. Puede que el dato exista "
              + "y esté fuera de tu alcance; en ese caso vas a necesitar pedírselo a quien "
              + "tenga acceso al ámbito correspondiente.";

        // LA SEGUNDA RAZÓN POR LA QUE UN RESULTADO PUEDE VENIR VACÍO, y hasta acá el
        // sistema sólo contaba la primera. «No hay nadie con ese título» y «nadie
        // cargó su formación» son el mismo cero, y decir el primero cuando la verdad
        // es el segundo afirma algo falso sobre personas reales.
        //
        // Va DESPUÉS del texto de alcance y no en su lugar: los dos límites pueden
        // ser ciertos a la vez, y quien pregunta necesita los dos para saber qué
        // hacer —pedir acceso, o pedirle a la gente que cargue el dato—.
        return cobertura is null ? texto : $"{texto} {cobertura.Frase()}";
    }

    /// <summary>
    /// Aclaración que acompaña al razonamiento de un turno que no devolvió filas.
    /// </summary>
    /// <remarks>
    /// Habla de lo que se buscó y de lo que se encontró, sin nombrar consulta,
    /// esquema ni tablas: la lee el usuario final, igual que el resto de los
    /// textos de esta clase (D15).
    /// </remarks>
    public const string AclaracionDeRazonamientoSinFilas =
        "Eso es lo que busqué, no lo que encontré: la búsqueda no devolvió ningún resultado.";

    /// <summary>
    /// El razonamiento de un turno que terminó sin filas.
    /// </summary>
    /// <remarks>
    /// <b>EL RAZONAMIENTO SE ESCRIBE ANTES DE EJECUTAR LA CONSULTA.</b> Sale de la
    /// primera llamada al modelo, junto con el SQL, así que sólo puede describir lo
    /// que el modelo se propuso hacer. Cuando además promete el resultado —«y
    /// devuelvo tres materias distintas»— y la consulta vuelve vacía, el turno
    /// afirma dos cosas incompatibles: el texto principal dice que no encontró
    /// nada, y la explicación dice que devolvió tres. De las dos, la que suena
    /// informada es la falsa.
    ///
    /// Pasó de verdad, y el caso muestra por qué importa: el modelo escribió el
    /// literal «Ingeniería Informática» contra una carrera que se llama «Ingeniería
    /// en Informática». Cero filas por un literal mal escrito, con una explicación
    /// que seguía prometiendo tres materias. Quien lee concluye que no hay
    /// profesores en esa carrera.
    ///
    /// <b>La aclaración se agrega y el texto del modelo NO se descarta.</b> Es lo
    /// único que deja ver cómo se leyó la pregunta, y fue exactamente lo que
    /// permitió encontrar el literal mal escrito. Se le saca la promesa, no la
    /// información.
    ///
    /// <b>Va acá y no en el prompt</b>, por el mismo motivo que
    /// <see cref="TextoDeResultadoVacio"/>: el prompt también se endureció —ver
    /// <c>RenderizadorDeEsquema</c>—, pero eso es un pedido y esto es una garantía.
    /// Un modelo que igual promete filas encuentra acá el desmentido.
    /// </remarks>
    public static string RazonamientoDeResultadoVacio(string razonamiento) =>
        // Sin razonamiento no hay nada que aclarar, y una aclaración suelta sería
        // ruido: el usuario ya leyó que no se encontró nada dos renglones arriba.
        string.IsNullOrWhiteSpace(razonamiento)
            ? razonamiento
            : $"{razonamiento.TrimEnd()} {AclaracionDeRazonamientoSinFilas}";

    /// <summary>
    /// Demostrativos que dejan una referencia sin resolver.
    /// </summary>
    /// <remarks>
    /// <b>NO ES la lista de <c>DetectorDeCambioDeTema</c>, y no hay que
    /// «deduplicarlas».</b> Aquélla incluye <c>el</c>, <c>los</c>, <c>las</c> y
    /// <c>lo</c> a propósito, porque su pregunta es «¿esto se ata de algún modo a lo
    /// anterior?» y ante la duda conviene NO pivotar. La pregunta de acá es otra
    /// —«¿esto quedó sin resolver?»— y con esa lista marcaría casi toda frase en
    /// español: «los profesores de Ingeniería de Software» lleva <c>los</c> y está
    /// perfectamente resuelta.
    ///
    /// Son demostrativos y referencias de posición, sin artículos ni pronombres
    /// átonos. Ya normalizados: sin acentos y en minúscula.
    /// </remarks>
    private static readonly HashSet<string> Demostrativos = new(StringComparer.Ordinal)
    {
        "eso", "esa", "ese", "esos", "esas", "esto", "estos", "estas",
        "aquel", "aquella", "aquellos", "aquellas",
        "dicho", "dicha", "dichos", "dichas",
        "mismo", "misma", "mismos", "mismas",
        "anterior", "anteriores", "ultimo", "ultima", "ultimos", "ultimas",
    };

    /// <summary>
    /// Si la pregunta todavía apunta a algo que no nombra.
    /// </summary>
    /// <remarks>
    /// <b>Cierra un contrato que nadie verificaba.</b> El reescritor promete «una
    /// pregunta que se entienda sola», y a veces devuelve algo que no se entiende
    /// solo: expande la anáfora sin resolverla —«los profesores de esa materia
    /// mencionada entre las 3 materias»— porque el hilo no tenía con qué. El
    /// generador se abstiene, que es lo correcto sobre una pregunta que no se
    /// entiende, pero el texto genérico hace pensar que el dato no existe.
    ///
    /// <b>Es una señal, no un veredicto.</b> Por sí sola no decide nada: una
    /// pregunta con demostrativo que el generador SÍ pudo contestar se contesta y
    /// listo. Sólo cambia el texto cuando además hubo abstención, y por eso no
    /// importa que sea generosa.
    /// </remarks>
    public static bool HayReferenciaSinResolver(string pregunta) =>
        !string.IsNullOrWhiteSpace(pregunta)
        && NormalizadorLexico.Palabras(pregunta).Any(Demostrativos.Contains);

    /// <summary>
    /// Texto de un seguimiento cuya referencia no se pudo resolver.
    /// </summary>
    /// <remarks>
    /// Dice lo que pasó de verdad y pide lo único que destraba el turno. El
    /// genérico —«no puedo responder eso con la información que tengo
    /// disponible»— es una afirmación sobre los DATOS, y acá el problema es la
    /// pregunta: quien lo lee concluye que el dato no existe.
    /// </remarks>
    public const string TextoReferenciaSinResolver =
        "No pude entender a qué te referís. Nombralo y vuelvo a intentar: "
        + "por ejemplo, en lugar de «esa materia», la materia por su nombre.";

    /// <summary>Texto de una pregunta que el esquema no cubre (caso 1).</summary>
    /// <remarks>
    /// No enumera qué tablas o columnas existen. Un rechazo que dijera «no existe
    /// tal columna» le confirma a quien pregunta cuáles sí existen, que es
    /// enumeración por mensaje de error (D15).
    /// </remarks>
    public const string TextoNoContestable =
        "No puedo responder eso con la información que tengo disponible.";

    /// <summary>Texto de una consulta rechazada por el validador (caso 5).</summary>
    /// <remarks>
    /// Dice lo mismo que el caso 1 y a propósito: la diferencia entre «no supe
    /// traducirla» y «la traduje mal» es interna, y contarla no le sirve a quien
    /// pregunta. Sin reintento ciego: volver a generar sobre el mismo prompt
    /// gasta una llamada para obtener, con alta probabilidad, lo mismo.
    /// </remarks>
    public const string TextoRechazadaPorValidador =
        "No pude armar una consulta segura para esa pregunta. Probá formulándola de otra manera.";

    /// <summary>Texto de una lectura que el motor rechazó por falta de privilegio.</summary>
    /// <remarks>
    /// Es el caso en que la defensa de más abajo hizo exactamente lo suyo: el actor
    /// pidió una columna que su rol no puede leer y PostgreSQL rechazó la consulta.
    /// Que funcione no alcanza — sin este texto, la excepción escapaba del turno y
    /// llegaba cruda a quien llamara, con el nombre de la tabla adentro.
    ///
    /// Dice «no tenés acceso» y no «no hay»: son cosas distintas y confundirlas es
    /// justamente lo que la política de abstención existe para evitar.
    /// </remarks>
    public const string TextoSinAccesoALosDatos =
        "No tenés acceso a esa información con tus permisos actuales.";

    /// <summary>Texto de un rechazo del motor que no es de permisos.</summary>
    /// <remarks>
    /// Una consulta que el validador dejó pasar y el motor no pudo ejecutar: SQL
    /// mal formada que el validador no atrapa, un tipo incompatible, un timeout de
    /// sentencia. Nunca se muestra el mensaje del motor, que nombra tablas y
    /// columnas.
    /// </remarks>
    public const string TextoErrorAlConsultar =
        "No pude completar esa consulta. Probá formulándola de otra manera.";

    /// <summary>Texto de proveedor caído (caso 6).</summary>
    /// <remarks>
    /// No expone el error crudo del proveedor. Tampoco promete un plazo: nadie sabe
    /// cuánto tarda en volver un servicio de terceros, y «en unos minutos» dicho sin
    /// saberlo es una promesa que el sistema no puede cumplir.
    /// </remarks>
    public const string TextoServicioDegradado =
        "El asistente no está disponible en este momento. Volvé a intentar más tarde.";

    /// <summary>
    /// Texto de cuota agotada (caso 6, la otra mitad).
    /// </summary>
    /// <remarks>
    /// Este caso arrancó compartiendo texto con el de proveedor caído, con el
    /// argumento de que desde el lado de quien pregunta los dos se arreglan
    /// esperando. Con la cuota implementada, el argumento no se sostiene: acá el
    /// sistema <b>sabe</b> exactamente cuándo vuelve el cupo, y callárselo deja a
    /// quien pregunta reintentando a ciegas contra algo que no se va a destrabar
    /// hasta una hora fija.
    /// </remarks>
    public static string TextoCuotaAgotada(DateTimeOffset? vuelveA) => vuelveA is { } cuando
        ? "Alcanzaste tu límite de consultas por ahora. Volvés a tener disponibles a las "
            + $"{cuando.ToLocalTime():HH:mm}."
        : "Alcanzaste tu límite de consultas por ahora. Probá de nuevo más tarde.";

    /// <summary>
    /// Los límites del asistente, tal como se los cuenta al usuario (RF-04).
    /// </summary>
    /// <remarks>
    /// Son literales, y conviene decir por qué cuando todo el resto del catálogo de
    /// capacidades se deriva de la base: <b>no son datos, son propiedades del
    /// sistema</b>. «No escribe» no sale de ningún GRANT que se pueda consultar —sale
    /// de que no existe ningún GRANT de escritura, que es una ausencia—, y «no
    /// consulta fuentes externas» sale de que no hay ninguna integración. Derivarlos
    /// exigiría inferir una negación de la falta de evidencia.
    ///
    /// Los tres están verificados por otros tests: el primero por los guards de
    /// arquitectura del módulo, el segundo por la única referencia de proyecto del
    /// csproj, y el tercero por los tests de RLS y de privilegios por columna.
    ///
    /// <b>Se acortaron sin perder ninguno.</b> Eran cuatro renglones de dos líneas
    /// cada uno y nadie los leía. Lo único que conserva su segunda oración es el
    /// tercero —«si algo no aparece, puede existir fuera de tu alcance»— porque no
    /// es una aclaración sino la advertencia central del sistema: es lo que impide
    /// leer un resultado vacío como un hecho, y es la misma distinción que
    /// <see cref="TextoDeResultadoVacio"/> sostiene en cada respuesta.
    /// </remarks>
    public static readonly IReadOnlyList<string> LimitesDelAsistente =
    [
        "No modifica nada: solo consulta.",
        "Solo lee este sistema: no Guaraní, ni planillas, ni otras fuentes.",
        "Solo ve lo que tu rol ya puede ver: si algo no aparece, puede existir fuera de tu alcance.",
        "No inventa: si no puede responder, lo dice.",
    ];

    /// <summary>Cómo se le describe al actor el alcance de lo que ve.</summary>
    public static string TextoDeAlcance(bool esGlobal) => esGlobal
        ? "Ves los datos de todo el Departamento."
        : "Ves los datos de tu ámbito: las materias y carreras que tenés asignadas.";

    /// <summary>
    /// Reglas que se agregan al prompt de redacción según el caso del turno.
    /// </summary>
    /// <remarks>
    /// Son las mismas prohibiciones que arriba, dichas al modelo. La duplicación
    /// es deliberada: los casos que se resuelven sin modelo están garantizados por
    /// código, y éstos —donde sí hay filas que narrar— dependen del prompt porque
    /// no hay otra forma de restringir una narración.
    /// </remarks>
    public static IReadOnlyList<string> ReglasDeRedaccion(
        bool alcanzaTodo,
        bool truncado,
        IReadOnlyList<CoberturaDeUnDato>? cobertura = null)
    {
        var reglas = new List<string>();

        foreach (var declarada in cobertura ?? [])
        {
            // El dato del portal es AUTODECLARADO y opcional. Sin esta regla, el
            // modelo narra las filas que ve como si fueran el Departamento entero:
            // «hay tres docentes con doctorado» cuando lo cierto es «tres de los
            // catorce que cargaron su formación».
            reglas.Add(
                $"{declarada.Frase()} Es un dato que cada docente carga sobre sí mismo y "
                + "la mayoría no lo hizo. NO presentes lo que ves como el total del "
                + "Departamento, y NO afirmes que nadie cumple una condición: decí "
                + "sobre cuántos se sabe.");
        }

        if (!alcanzaTodo)
        {
            reglas.Add(
                "El usuario ve solo una parte del Departamento. NO afirmes que algo no existe "
                + "ni que no hay más casos: lo que estás viendo es lo que él puede ver, no todo "
                + "lo que hay. Encuadrá la respuesta en su alcance.");
        }

        if (truncado)
        {
            reglas.Add(
                "El resultado se recortó. NO afirmes ningún total ni ningún conteo: "
                + "decí que hay más y mostrá lo que llegó. NUNCA digas cuántos quedaron afuera.");
        }

        return reglas;
    }
}
