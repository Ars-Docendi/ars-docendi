using System.Globalization;
using System.Text;
using ArsDocendi.Evaluacion.Nucleo.Puntuacion;

namespace ArsDocendi.Evaluacion.Nucleo.Runner;

/// <summary>Qué encontró el gate al comparar.</summary>
public sealed record VeredictoDelGate(
    bool Pasa,
    IReadOnlyList<string> Regresiones,
    IReadOnlyList<string> Mejoras,
    IReadOnlyList<string> Nuevos,
    string? ExigeRegenerar,
    IReadOnlyList<string>? Oscilaciones = null)
{
    /// <summary>
    /// Ítems inestables que pasaron de acierto a falla: se informan, no hacen fallar el gate.
    /// </summary>
    public IReadOnlyList<string> Tolerados { get; } = Oscilaciones ?? [];

    /// <summary>Un resumen legible de lo que pasó.</summary>
    public string Renderizar()
    {
        if (ExigeRegenerar is not null)
        {
            return $"El gate no comparó: {ExigeRegenerar}";
        }

        var texto = new StringBuilder();
        texto.Append(CultureInfo.InvariantCulture, $"Gate: {(Pasa ? "PASA" : "FALLA")}.");

        if (Regresiones.Count > 0)
        {
            texto.Append(CultureInfo.InvariantCulture,
                $" Regresiones: {string.Join(", ", Regresiones)}.");
        }

        if (Tolerados.Count > 0)
        {
            texto.Append(CultureInfo.InvariantCulture,
                $" Oscilaciones conocidas (no hacen fallar el gate; {Tolerados.Count} tolerada(s)): {string.Join(", ", Tolerados)}.");
        }

        if (Mejoras.Count > 0)
        {
            texto.Append(CultureInfo.InvariantCulture, $" Mejoras: {string.Join(", ", Mejoras)}.");
        }

        if (Nuevos.Count > 0)
        {
            texto.Append(CultureInfo.InvariantCulture, $" Nuevos: {string.Join(", ", Nuevos)}.");
        }

        return texto.ToString();
    }
}

/// <summary>
/// Compara una corrida contra su línea de base, ítem por ítem.
/// </summary>
/// <remarks>
/// <b>Lock por ítem y no umbral agregado, y el motivo es aritmético.</b> Con pocas
/// decenas de ítems puntuados, tres que se rompen y tres que se arreglan dan delta
/// cero y pasan cualquier umbral mientras el asistente cambió de comportamiento. Y un
/// solo ítem vale un par de puntos porcentuales: un umbral fino sería ruido y uno
/// grueso no detectaría nada.
///
/// Ventaja adicional, y no es menor: el lock <b>no depende del tamaño del
/// dataset</b>. Un dataset de esta escala tiene un intervalo de confianza de varios
/// puntos, así que ninguna comparación de agregados puede sostener una afirmación de
/// mejora o regresión. El lock esquiva el problema en vez de intentar resolverlo
/// creciendo el dataset.
/// </remarks>
public static class GateDeRegresion
{
    /// <summary>Compara el reporte contra la línea de base.</summary>
    /// <param name="reporte">La corrida.</param>
    /// <param name="linea">La línea de base del mismo eje.</param>
    /// <param name="inestables">
    /// Ítems del eje que oscilan en vivo (id → motivo). Una caída de acierto a falla en
    /// uno de ellos se informa como oscilación conocida y no hace fallar el gate. Los
    /// demás ítems conservan el lock estricto.
    /// </param>
    /// <exception cref="InvalidOperationException">Algún id inestable no está en la línea de base.</exception>
    public static VeredictoDelGate Comparar(
        Reporte reporte, LineaDeBase linea, IReadOnlyDictionary<string, string>? inestables = null)
    {
        ArgumentNullException.ThrowIfNull(reporte);
        ArgumentNullException.ThrowIfNull(linea);
        inestables ??= new Dictionary<string, string>(StringComparer.Ordinal);

        // LA LISTA NO PUEDE PUDRIRSE EN SILENCIO. Un id renombrado o borrado dejaría
        // de tolerar lo que se quería tolerar, o peor, quedaría un permiso colgado.
        // Se valida antes del sello: un id desconocido es un error del archivo, no
        // algo que el sello explique.
        var desconocidos = inestables.Keys.Where(id => !linea.Items.ContainsKey(id)).Order(StringComparer.Ordinal).ToArray();
        if (desconocidos.Length > 0)
        {
            throw new InvalidOperationException(
                $"inestables.json (eje {linea.Eje}): ids que no están en la línea de base: "
                + $"{string.Join(", ", desconocidos)}. Corregí o quitá esas entradas.");
        }

        var cambiado = SelloCambiado(reporte.Sello, linea.Sello);
        if (cambiado is not null)
        {
            // NO SE COMPARA. Los tres hashes identifican CONTRA QUÉ se midió: con
            // cualquiera distinto, comparar ítem a ítem sería comparar dos cosas que
            // no son la misma, y el resultado —pase o falle— no significaría nada.
            return new VeredictoDelGate(
                Pasa: false, [], [], [],
                $"cambió {cambiado}. Regenerá la línea de base y volvé a correr.");
        }

        var regresiones = new List<string>();
        var mejoras = new List<string>();
        var nuevos = new List<string>();
        var tolerados = new List<string>();

        foreach (var resultado in reporte.Resultados)
        {
            if (!linea.Items.TryGetValue(resultado.Id, out var antes))
            {
                // Un ítem nuevo no puede haber regresado: no tenía historia. Se
                // informa para que quien lea el gate sepa que el dataset creció.
                nuevos.Add(resultado.Id);
                continue;
            }

            var pasabaAntes = EsAcierto(antes);
            var pasaAhora = EsAcierto(resultado.Desenlace);

            if (pasabaAntes && !pasaAhora && inestables.TryGetValue(resultado.Id, out var motivo))
            {
                tolerados.Add($"{resultado.Id} ({antes} → {resultado.Desenlace}; {motivo})");
            }
            else if (pasabaAntes && !pasaAhora)
            {
                regresiones.Add($"{resultado.Id} ({antes} → {resultado.Desenlace})");
            }
            else if (!pasabaAntes && pasaAhora)
            {
                mejoras.Add(resultado.Id);
            }
        }

        return new VeredictoDelGate(regresiones.Count == 0, regresiones, mejoras, nuevos, null, tolerados);
    }

    /// <summary>
    /// Qué cuenta como «pasaba».
    /// </summary>
    /// <remarks>
    /// Solo los dos aciertos. La abstención sobre algo contestable no cuenta como
    /// pasar —es una falta de capacidad, aunque no reste—, así que un ítem que va de
    /// «tradujo bien» a «se abstuvo» ES una regresión y el gate la ve.
    /// </remarks>
    public static bool EsAcierto(DesenlaceDeItem desenlace) =>
        desenlace is DesenlaceDeItem.TraduccionCorrecta or DesenlaceDeItem.AbstencionCorrecta;

    private static string? SelloCambiado(SelloDeIdentidad ahora, SelloDeIdentidad antes)
    {
        if (!string.Equals(ahora.Prefijo, antes.Prefijo, StringComparison.Ordinal))
        {
            return "el prefijo del prompt";
        }

        if (!string.Equals(ahora.Dataset, antes.Dataset, StringComparison.Ordinal))
        {
            return "el dataset";
        }

        return string.Equals(ahora.Fixture, antes.Fixture, StringComparison.Ordinal)
            ? null
            : "el fixture";
    }
}
