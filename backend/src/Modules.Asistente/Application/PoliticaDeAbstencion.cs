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
    public static bool ConvieneReintentar(ResultadoDeConsulta resultado, bool actorEsGlobal)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        return resultado.EstaVacio && actorEsGlobal;
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
    public static string TextoDeResultadoVacio(bool actorEsGlobal) => actorEsGlobal
        ? "No encontré ningún registro que responda esa pregunta."
        : "No encontré nada dentro de lo que podés consultar. Puede que el dato exista "
          + "y esté fuera de tu alcance; en ese caso vas a necesitar pedírselo a quien "
          + "tenga acceso al ámbito correspondiente.";

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
    /// </remarks>
    public static readonly IReadOnlyList<string> LimitesDelAsistente =
    [
        "No modifica nada: solo consulta. No puede cargar, aprobar ni cambiar el estado de un trámite.",
        "Solo consulta datos de este sistema. No accede a Guaraní, a planillas ni a ninguna otra fuente.",
        "Solo te muestra lo que tu rol ya puede ver. Si algo no aparece, puede existir y estar fuera de tu alcance.",
        "No inventa: si no puede responder con lo que ve, lo dice.",
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
    public static IReadOnlyList<string> ReglasDeRedaccion(bool actorEsGlobal, bool truncado)
    {
        var reglas = new List<string>();

        if (!actorEsGlobal)
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
