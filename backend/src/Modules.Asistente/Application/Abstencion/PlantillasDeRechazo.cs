namespace Modules.Asistente.Application;

/// <summary>
/// Renderiza el texto de un rechazo declarado por el modelo, a partir de una
/// plantilla por motivo con escalación (design.md D1/D5/D6/D11 de
/// asistente-rechazos-dinamicos).
/// </summary>
/// <remarks>
/// <b>Nunca texto del modelo.</b> Lo único que puede aparecer en el cuerpo,
/// aparte de estas plantillas fijas, es el término ya validado por
/// <see cref="TerminoDelRechazo"/> —los caracteres del usuario, re-copiados—.
///
/// <b>Ninguna variante afirma ausencia.</b> Nunca corrió una consulta: decir
/// «no encontré nada sobre «python»» sería el mismo falso negativo que
/// <see cref="PoliticaDeAbstencion.AlcanzaTodo"/> ya documenta para el caso de
/// resultado vacío (design.md D6).
///
/// <b>La copia de acá es la aprobada por el PO en design.md D11, palabra por
/// palabra</b> (confirmado 2026-09-26): cada rama de cada motivo/variante
/// reproduce exactamente la celda de esa tabla, corchete por corchete —una
/// celda sin corchete de término se renderiza igual con o sin término
/// validado (<c>otro_sistema</c> variante 3, <c>muy_general</c> variante 3),
/// y <c>{areas}</c> sólo aparece donde la tabla lo tiene (variante 2 de las
/// cuatro razones, y variante 1 de <c>fuera_de_tema</c>). Los tests fijan
/// PROPIEDADES sobre esa copia fija —distinción por razón/variante, eco del
/// término sólo donde la celda lo admite, ninguna afirmación de ausencia,
/// ningún nombre de esquema, el puntero de ayuda enrutado a la
/// meta-pregunta— y no palabras sueltas; no pinnean una sentencia distinta de
/// la de D11.
/// </remarks>
internal static class PlantillasDeRechazo
{
    /// <summary>El pointer a la meta-pregunta, que resuelve el carril social a costo cero.</summary>
    internal const string Ayuda =
        "Si querés ver todo lo que puedo consultar, preguntame «¿qué podés hacer?».";

    /// <summary>
    /// El texto de un rechazo declarado por el modelo.
    /// </summary>
    /// <param name="motivo">El motivo declarado (o <see cref="MotivoDeRechazo.NoCubierto"/> por defecto).</param>
    /// <param name="termino">
    /// El término ya validado por <see cref="TerminoDelRechazo.Validar"/>, o
    /// <c>null</c> si no hay ninguno que citar.
    /// </param>
    /// <param name="areas">
    /// Las áreas consultables ya renderizadas por <see cref="EtiquetasDeAreas.Nombrar"/>,
    /// o <c>null</c> si el catálogo no se pudo leer.
    /// </param>
    /// <param name="rechazosPrevios">
    /// Cuántos rechazos anteriores tuvo esta conversación
    /// (<see cref="HiloConversacional.RechazosPrevios"/>), <c>0</c> en el
    /// primer rechazo o cuando no hay conversación (el evaluador).
    /// </param>
    public static string Texto(MotivoDeRechazo motivo, string? termino, string? areas, int rechazosPrevios)
    {
        var variante = Variante(rechazosPrevios);

        return motivo switch
        {
            MotivoDeRechazo.FueraDeTema => FueraDeTema(variante, termino, areas),
            MotivoDeRechazo.OtroSistema => OtroSistema(variante, termino, areas),
            MotivoDeRechazo.MuyGeneral => MuyGeneral(variante, termino, areas),
            _ => NoCubierto(variante, termino, areas),
        };
    }

    /// <summary>
    /// La variante según cuántos rechazos anteriores hubo (design.md D5):
    /// <c>1</c> en el primero, después alterna <c>2</c>/<c>3</c> — nunca repite
    /// entre dos rechazos consecutivos de la misma conversación.
    /// </summary>
    internal static int Variante(int rechazosPrevios) =>
        rechazosPrevios <= 0 ? 1 : 2 + ((rechazosPrevios - 1) % 2);

    private static string FueraDeTema(int variante, string? termino, string? areas) => variante switch
    {
        1 => termino is not null
            ? $"No puedo responder sobre «{termino}»{ClausulaTrabajoCon(areas)}"
            : $"No puedo responder eso{ClausulaTrabajoCon(areas)}",
        2 => termino is not null
            ? $"«{termino}» también queda fuera de lo que consulto.{ClausulaSoloTrabajo(areas)}"
            : $"Esa pregunta también queda fuera de lo que consulto.{ClausulaSoloTrabajo(areas)}",
        _ => termino is not null
            ? $"Sigo sin poder ayudarte con eso («{termino}»). {Ayuda}"
            : $"Sigo sin poder ayudarte con eso. {Ayuda}",
    };

    private static string ClausulaTrabajoCon(string? areas) =>
        areas is not null ? $" con los datos que consulto: trabajo con {areas}." : $" con los datos que consulto. {Ayuda}";

    private static string ClausulaSoloTrabajo(string? areas) => areas is not null
        ? $" Solo trabajo con {areas}; si es sobre eso, reformulala y la busco."
        : $" {Ayuda}";

    /// <summary>
    /// El límite de fuentes en primera persona, como habla el resto del rechazo.
    /// </summary>
    /// <remarks>
    /// No reusa <see cref="PoliticaDeAbstencion.LimitesDelAsistente"/>: esa lista
    /// describe al asistente en tercera persona («Solo lee este sistema…») para la
    /// ayuda, y pegada a «No puedo responder…» mezclaría las dos voces. Nombra los
    /// mismos sistemas; el test (g) de <c>PlantillasDeRechazoTests</c> impide que
    /// aparezca uno que no esté en esa lista.
    /// </remarks>
    internal const string LimiteDeFuentes =
        "Solo leo este sistema: no Guaraní, ni planillas, ni otras fuentes.";

    private static string OtroSistema(int variante, string? termino, string? areas)
    {
        var limite = LimiteDeFuentes;

        return variante switch
        {
            1 => termino is not null
                ? $"«{termino}» parece estar en otro sistema. {limite}"
                : $"Eso parece estar en otro sistema. {limite}",
            2 => termino is not null
                ? $"Eso («{termino}») también parece venir de otro sistema. {limite}{ClausulaLoQueSiConsulto(areas)}"
                : $"Eso también parece venir de otro sistema. {limite}{ClausulaLoQueSiConsulto(areas)}",
            // VARIANTE 3 SIN FORMA CON TÉRMINO (design.md D11): la tabla no le
            // pone corchetes — a diferencia de las otras tres variantes-3—, así
            // que se renderiza igual exista o no un término validado.
            _ => $"Esa información no la leo desde acá. {Ayuda}",
        };
    }

    private static string ClausulaLoQueSiConsulto(string? areas) =>
        areas is not null ? $" Lo que sí consulto: {areas}." : $" {Ayuda}";

    private static string MuyGeneral(int variante, string? termino, string? areas) => variante switch
    {
        1 => termino is not null
            ? $"La pregunta sobre «{termino}» es muy amplia para una consulta. Contame qué dato "
                + "puntual buscás y de qué carrera, materia o período."
            : "La pregunta es muy amplia para una consulta. Contame qué dato puntual buscás y de "
                + "qué carrera, materia o período.",
        2 => termino is not null
            ? $"Necesito una pregunta más concreta sobre «{termino}».{ClausulaPuedoConsultar(areas)}"
            : $"Necesito una pregunta más concreta.{ClausulaPuedoConsultar(areas)}",
        // VARIANTE 3 SIN FORMA CON TÉRMINO (design.md D11): sin corchetes en la
        // tabla, se renderiza igual exista o no un término validado.
        _ => "Todavía me falta precisión para armar la consulta. " + Ayuda,
    };

    private static string ClausulaPuedoConsultar(string? areas) => areas is not null
        ? $" Puedo consultar {areas}: decime qué dato de eso te sirve."
        : $" {Ayuda}";

    private static string NoCubierto(int variante, string? termino, string? areas) => variante switch
    {
        // Variante 1, sin término: TEXTO GENÉRICO PREEXISTENTE, PALABRA POR
        // PALABRA (design.md D11) — así los tests y el evaluador que ya
        // esperaban `PoliticaDeAbstencion.TextoNoContestable` siguen viendo lo
        // mismo, y los cassettes viejos (sin `motivo`) resuelven exactamente
        // como resolvían antes de este cambio.
        1 => termino is not null
            ? $"No puedo responder sobre «{termino}» con la información que tengo disponible."
            : PoliticaDeAbstencion.TextoNoContestable,
        2 => termino is not null
            ? $"Tampoco puedo responder eso sobre «{termino}» con lo que consulto.{ClausulaLoQueSiTengo(areas)}"
            : $"Tampoco puedo responder eso con lo que consulto.{ClausulaLoQueSiTengo(areas)}",
        _ => termino is not null
            ? $"Eso («{termino}») sigue fuera de lo que puedo consultar. {Ayuda}"
            : $"Eso sigue fuera de lo que puedo consultar. {Ayuda}",
    };

    private static string ClausulaLoQueSiTengo(string? areas) =>
        areas is not null ? $" Lo que sí tengo: {areas}." : $" {Ayuda}";
}
