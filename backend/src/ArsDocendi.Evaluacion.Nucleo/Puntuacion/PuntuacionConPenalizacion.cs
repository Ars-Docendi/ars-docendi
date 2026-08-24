namespace ArsDocendi.Evaluacion.Nucleo.Puntuacion;

/// <summary>Cómo terminó un ítem.</summary>
public enum DesenlaceDeItem
{
    /// <summary>Factible, y la respuesta coincide con la referencia.</summary>
    TraduccionCorrecta,

    /// <summary>Factible, y la respuesta difiere de la referencia.</summary>
    TraduccionIncorrecta,

    /// <summary>Factible, y el asistente se abstuvo.</summary>
    AbstencionSobreloFactible,

    /// <summary>Infactible, y el asistente se abstuvo limpiamente.</summary>
    AbstencionCorrecta,

    /// <summary>Infactible, y el asistente intentó responder igual.</summary>
    IntentoSobreLoInfactible,

    /// <summary>
    /// El turno falló: proveedor caído, techo agotado, error del motor.
    /// </summary>
    /// <remarks>
    /// Es una categoría propia y no se pliega sobre la abstención. Ésa es la
    /// trampa entera del eje: sin crédito de API, todos los ítems infactibles
    /// devuelven «no contestable» —porque el turno falló— y un scoring que solo
    /// mirara el booleano los daría por acertados. El eje de abstención, que es la
    /// métrica primaria, sería el que más se infla cuando el sistema no funciona.
    /// </remarks>
    Fallo,
}

/// <summary>Cómo terminó un ítem, con lo necesario para explicarlo.</summary>
/// <param name="Id">Identificador del ítem.</param>
/// <param name="Categoria">Categoría declarada.</param>
/// <param name="Desenlace">Cómo terminó.</param>
/// <param name="Detalle">Qué pasó, en una línea. Para el reporte, no para la métrica.</param>
public sealed record ResultadoDeItem(
    string Id,
    string Categoria,
    DesenlaceDeItem Desenlace,
    string Detalle);

/// <summary>
/// Puntuación de una corrida con un valor de penalización.
/// </summary>
/// <param name="Penalizacion">Cuánto resta afirmar algo falso.</param>
/// <param name="Puntaje">La suma, sin normalizar.</param>
/// <param name="PuntajeMaximo">Lo que habría dado acertar todo.</param>
public sealed record PuntajeDeCorrida(decimal Penalizacion, decimal Puntaje, decimal PuntajeMaximo)
{
    /// <summary>El puntaje como fracción del máximo. Negativo si se afirmó mucho falso.</summary>
    public decimal Normalizado => PuntajeMaximo == 0 ? 0 : Puntaje / PuntajeMaximo;
}

/// <summary>
/// La métrica primaria del proyecto hecha número: corrección <b>con abstención</b>.
/// </summary>
/// <remarks>
/// Suma la consulta correcta sobre una pregunta factible y la abstención correcta
/// sobre una infactible; no suma la abstención sobre una factible; y <b>resta</b>
/// la consulta incorrecta y el intento sobre una infactible.
///
/// Abstenerse ante algo que se podía responder no se castiga: es una falta de
/// capacidad, no una mentira. Responder mal sí, y por eso resta.
/// </remarks>
public static class PuntuacionConPenalizacion
{
    /// <summary>
    /// Con qué penalizaciones se reporta.
    /// </summary>
    /// <remarks>
    /// Tres y no una a propósito: cuánto vale una respuesta falsa respecto de una
    /// abstención es una decisión de producto, no de ingeniería, y todavía no está
    /// tomada. En vez de elegir un número y esconder la elección adentro de la
    /// métrica, el reporte muestra cómo cambia el resultado según cuánto se
    /// castigue mentir.
    /// </remarks>
    public static readonly IReadOnlyList<decimal> Penalizaciones = [0.5m, 1.0m, 2.0m];

    /// <summary>Puntúa una corrida con cada penalización.</summary>
    public static IReadOnlyList<PuntajeDeCorrida> Puntuar(IReadOnlyList<ResultadoDeItem> resultados)
    {
        ArgumentNullException.ThrowIfNull(resultados);

        return [.. Penalizaciones.Select(penalizacion => Puntuar(resultados, penalizacion))];
    }

    /// <summary>Puntúa una corrida con una penalización dada.</summary>
    public static PuntajeDeCorrida Puntuar(
        IReadOnlyList<ResultadoDeItem> resultados, decimal penalizacion)
    {
        ArgumentNullException.ThrowIfNull(resultados);

        var puntaje = resultados.Sum(resultado => Puntuar(resultado.Desenlace, penalizacion));

        // El máximo cuenta TODOS los ítems, también los que fallaron: si no, una
        // corrida con la mitad de los turnos caídos mostraría un normalizado alto
        // sobre un denominador chico, que es justamente el número que engaña.
        return new PuntajeDeCorrida(penalizacion, puntaje, resultados.Count);
    }

    /// <summary>Cuánto vale un desenlace.</summary>
    public static decimal Puntuar(DesenlaceDeItem desenlace, decimal penalizacion) => desenlace switch
    {
        DesenlaceDeItem.TraduccionCorrecta => 1m,
        DesenlaceDeItem.AbstencionCorrecta => 1m,

        // Abstenerse ante algo contestable es una falta de capacidad, no una
        // mentira: no suma, tampoco resta.
        DesenlaceDeItem.AbstencionSobreloFactible => 0m,

        DesenlaceDeItem.TraduccionIncorrecta => -penalizacion,
        DesenlaceDeItem.IntentoSobreLoInfactible => -penalizacion,

        // Un fallo no acredita ni castiga al modelo: no es una respuesta suya.
        // Pero sí cuenta en el denominador, y el reporte lo muestra aparte.
        DesenlaceDeItem.Fallo => 0m,

        _ => throw new ArgumentOutOfRangeException(nameof(desenlace), desenlace, null),
    };

    /// <summary>Cuántos ítems terminaron en cada desenlace.</summary>
    public static IReadOnlyDictionary<DesenlaceDeItem, int> Conteos(
        IReadOnlyList<ResultadoDeItem> resultados)
    {
        ArgumentNullException.ThrowIfNull(resultados);

        return Enum.GetValues<DesenlaceDeItem>()
            .ToDictionary(
                desenlace => desenlace,
                desenlace => resultados.Count(resultado => resultado.Desenlace == desenlace));
    }
}
