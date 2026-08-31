namespace Modules.Asistente.Application;

/// <summary>
/// Una intención reconocida con todos sus slots resueltos.
/// </summary>
/// <param name="Intencion">Cuál del catálogo.</param>
/// <param name="Destino">A dónde enrutaría. Copiado de la intención, para el llamador.</param>
/// <param name="Slots">Cada slot exigido con el valor único al que resolvió.</param>
public sealed record IntencionResuelta(
    Intencion Intencion,
    string Destino,
    IReadOnlyDictionary<string, ValorDeSlot> Slots);

/// <summary>
/// Reconoce intenciones del catálogo cerrado y resuelve sus slots contra la base.
/// </summary>
/// <remarks>
/// <b>Cuesta cero llamadas al modelo.</b> Todo lo que hace es comparar conjuntos de
/// términos y buscar en un diccionario ya cargado.
///
/// <b>Clasificar la intención con el modelo está descartado con evidencia</b>: 60%
/// de F1 en triage de cinco clases, 77,4% en nueve vías. Un clasificador que falla
/// una de cada cuatro veces, cuesta una llamada y corta el flujo es peor que una
/// tabla.
///
/// <b>Y no generaliza, que conviene decirlo.</b> La guarda que hace viable el
/// enrutador social —interceptar solo si no queda ningún token de contenido— no
/// sirve acá: distinguir «¿cuál es el estado del pedido de Pérez?» de una pregunta
/// arbitraria exige intención Y slots. Es viable sobre un catálogo chico de
/// preguntas frecuentes, y crece de a una intención con su caso de prueba.
/// </remarks>
public sealed class ResolutorDeIntenciones(
    CatalogoDeIntenciones catalogo, ICatalogoDelDominio dominio)
{
    /// <summary>
    /// La primera intención cuyos términos están todos y cuyos slots resuelven
    /// todos, o nulo.
    /// </summary>
    /// <remarks>
    /// <b>Nulo no es un error</b>: es el caso normal, y significa que la pregunta
    /// sigue al carril SQL. El default es SQL y nunca API, porque enrutar mal hacia
    /// la API devuelve cero filas y «cero filas» es indistinguible de «no hay» —la
    /// mentira que la política de abstención prohíbe—. Fallar hacia el carril más
    /// caro es fallar hacia el que puede responder.
    /// </remarks>
    public async Task<IntencionResuelta?> ResolverAsync(string pregunta, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pregunta);

        var terminos = NormalizadorLexico.Terminos(pregunta);
        var candidatas = catalogo.Intenciones.Where(i => i.Terminos.IsSubsetOf(terminos)).ToList();

        if (candidatas.Count == 0)
        {
            return null;
        }

        // El catálogo se consulta recién acá, y no antes de mirar los términos: sin
        // ninguna candidata no hay nada que resolver, y esta es la única línea del
        // resolutor que puede tocar la base.
        var valores = await dominio.ObtenerAsync(ct);

        // Enmarcada con espacios en las puntas para que el término se busque entero:
        // sin los marcos, «datos» encontraría a «bases de datos» por adentro. Es el
        // mismo enmarcado que usa el detector de ambigüedad.
        var enmarcada = $" {string.Join(' ', NormalizadorLexico.Palabras(pregunta))} ";

        foreach (var intencion in candidatas)
        {
            var resuelta = Resolver(intencion, enmarcada, valores);

            if (resuelta is not null)
            {
                return resuelta;
            }
        }

        return null;
    }

    private static IntencionResuelta? Resolver(
        Intencion intencion, string enmarcada, CatalogoDelDominio valores)
    {
        var resueltos = new Dictionary<string, ValorDeSlot>(StringComparer.Ordinal);

        foreach (var slot in intencion.Slots)
        {
            var mencionados = Mencionados(slot.Clase, enmarcada, valores);

            // Ni cero ni dos. Cero es que la pregunta no nombra nada de esa clase;
            // dos es que nombra dos cosas distintas y elegir una sería adivinar. Un
            // término que colisiona ya vino como nulo desde el catálogo y cuenta
            // como sin resolver. Las tres veces la respuesta es la misma.
            if (mencionados.Count != 1 || mencionados[0] is null)
            {
                return null;
            }

            resueltos[slot.Nombre] = mencionados[0]!;
        }

        return new IntencionResuelta(intencion, intencion.Destino, resueltos);
    }

    /// <summary>
    /// Los valores de esa clase que la pregunta nombra, uno por término mencionado.
    /// </summary>
    /// <remarks>
    /// Un término mencionado que colisiona entra como <c>null</c> y no se descarta:
    /// descartarlo dejaría que «el pedido de López o el de Gómez» resolviera a Gómez
    /// ignorando que López era ambiguo, que es exactamente la adivinanza que la
    /// regla prohíbe.
    ///
    /// Los términos vienen de más largo a más corto, y los que son parte de uno ya
    /// encontrado se saltean: si «bases de datos» matcheó, «datos» no vuelve a
    /// contar como una segunda materia.
    /// </remarks>
    private static List<ValorDeSlot?> Mencionados(
        ClaseDeSlot clase, string enmarcada, CatalogoDelDominio valores)
    {
        var encontrados = new List<ValorDeSlot?>();
        var cubiertos = new List<string>();

        foreach (var termino in valores.TerminosDe(clase))
        {
            if (!enmarcada.Contains($" {termino} ", StringComparison.Ordinal))
            {
                continue;
            }

            if (cubiertos.Any(largo =>
                largo.Contains($" {termino} ", StringComparison.Ordinal)
                || largo.StartsWith($"{termino} ", StringComparison.Ordinal)
                || largo.EndsWith($" {termino}", StringComparison.Ordinal)))
            {
                continue;
            }

            cubiertos.Add(termino);
            encontrados.Add(valores.Unico(clase, termino));
        }

        return encontrados;
    }
}
