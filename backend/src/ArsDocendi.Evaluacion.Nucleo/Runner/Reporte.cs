using System.Globalization;
using System.Text;
using ArsDocendi.Evaluacion.Nucleo.Puntuacion;

namespace ArsDocendi.Evaluacion.Nucleo.Runner;

/// <summary>
/// Los tres hashes con que se sella un reporte.
/// </summary>
/// <param name="Prefijo">Huella del prefijo del prompt de sistema.</param>
/// <param name="Dataset">Huella del archivo del dataset.</param>
/// <param name="Fixture">Huella del fixture.</param>
/// <remarks>
/// Sin el sello, el problema se repite en el próximo refactor de esquema: los
/// reportes quedan describiendo datasets que ya no existen y los números del
/// proyecto dejan de ser reproducibles, sin que nadie lo note. Con el sello, una
/// corrida vieja se identifica como vieja en lugar de compararse de igual a igual
/// con una nueva.
/// </remarks>
public sealed record SelloDeIdentidad(string Prefijo, string Dataset, string Fixture);

/// <summary>
/// El reporte de una corrida.
/// </summary>
/// <remarks>
/// Se genera; no se edita a mano. Los conteos salen del dataset efectivamente
/// cargado, no de prosa: un reporte cuyos números no se derivan de lo que se
/// ejecutó es un reporte que puede mentir sin que nada falle.
/// </remarks>
public sealed record Reporte(
    string Eje,
    SelloDeIdentidad Sello,
    IReadOnlyList<ResultadoDeItem> Resultados,
    IReadOnlyDictionary<string, int> ConteoPorCategoria)
{
    /// <summary>Total de ítems evaluados.</summary>
    public int Total => Resultados.Count;

    /// <summary>Los tres puntajes.</summary>
    public IReadOnlyList<PuntajeDeCorrida> Puntajes => PuntuacionConPenalizacion.Puntuar(Resultados);

    /// <summary>Cuántos ítems terminaron en cada desenlace.</summary>
    public IReadOnlyDictionary<DesenlaceDeItem, int> Conteos =>
        PuntuacionConPenalizacion.Conteos(Resultados);

    /// <summary>Renderiza el reporte en Markdown.</summary>
    public string Renderizar()
    {
        var texto = new StringBuilder();

        texto.Append(CultureInfo.InvariantCulture, $"# Evaluación — eje de {Eje}\n\n");
        texto.Append("> Reporte **generado**. No editar a mano.\n\n");

        texto.Append("## Sello de identidad\n\n");
        texto.Append("| Qué                  | Huella |\n");
        texto.Append("| -------------------- | ------ |\n");
        texto.Append(CultureInfo.InvariantCulture, $"| Prefijo del prompt   | `{Sello.Prefijo}` |\n");
        texto.Append(CultureInfo.InvariantCulture, $"| Dataset              | `{Sello.Dataset}` |\n");
        texto.Append(CultureInfo.InvariantCulture, $"| Fixture              | `{Sello.Fixture}` |\n\n");

        EscribirPuntajes(texto);
        EscribirDesenlaces(texto);
        EscribirCategorias(texto);
        EscribirItems(texto);

        return texto.ToString();
    }

    private void EscribirPuntajes(StringBuilder texto)
    {
        texto.Append("## Corrección con abstención\n\n");
        texto.Append(
            "Suma la traducción correcta y la abstención correcta; la abstención sobre algo\n"
            + "contestable no suma ni resta; la traducción incorrecta y el intento sobre algo\n"
            + "infactible **restan** la penalización.\n\n");
        texto.Append(
            "Se reporta con tres penalizaciones porque cuánto vale una respuesta falsa frente\n"
            + "a una abstención es una decisión de producto, no de ingeniería.\n\n");

        texto.Append("| Penalización | Puntaje | Máximo | Normalizado |\n");
        texto.Append("| ------------ | ------- | ------ | ----------- |\n");

        foreach (var puntaje in Puntajes)
        {
            texto.Append(CultureInfo.InvariantCulture,
                $"| {puntaje.Penalizacion:0.0} | {puntaje.Puntaje:0.00} | "
                + $"{puntaje.PuntajeMaximo:0} | {puntaje.Normalizado:P1} |\n");
        }

        texto.Append('\n');
    }

    private void EscribirDesenlaces(StringBuilder texto)
    {
        texto.Append("## Desenlaces\n\n");
        texto.Append("| Desenlace | Ítems |\n");
        texto.Append("| --------- | ----- |\n");

        foreach (var (desenlace, cuantos) in Conteos)
        {
            texto.Append(CultureInfo.InvariantCulture, $"| {Nombrar(desenlace)} | {cuantos} |\n");
        }

        texto.Append(CultureInfo.InvariantCulture, $"| **Total** | **{Total}** |\n\n");

        var fallos = Conteos[DesenlaceDeItem.Fallo];
        if (fallos > 0)
        {
            texto.Append(CultureInfo.InvariantCulture, $"> **{fallos} ítem(s) fallaron.** ");
            texto.Append(
                "Un fallo no acredita ni castiga al modelo, pero\n"
                + "> sí cuenta en el denominador. Un número alto acá invalida la corrida.\n\n");
        }

        var truncados = Conteos[DesenlaceDeItem.GeneracionTruncada];
        if (truncados > 0)
        {
            texto.Append(CultureInfo.InvariantCulture,
                $"> **{truncados} generación(es) se cortaron por el techo de tokens.** ");
            texto.Append(
                "No acreditan ni castigan,\n"
                + "> pero cuentan en el denominador: el modelo no llegó a decidir. Subí\n"
                + "> `MaximoDeTokensDeGeneracion` o bajá el esfuerzo antes de comparar corridas.\n\n");
        }
    }

    private void EscribirCategorias(StringBuilder texto)
    {
        texto.Append("## Ítems por categoría\n\n");
        texto.Append("| Categoría | Ítems |\n");
        texto.Append("| --------- | ----- |\n");

        foreach (var (categoria, cuantos) in ConteoPorCategoria)
        {
            texto.Append(CultureInfo.InvariantCulture, $"| `{categoria}` | {cuantos} |\n");
        }

        texto.Append('\n');
    }

    private void EscribirItems(StringBuilder texto)
    {
        texto.Append("## Detalle\n\n");
        texto.Append("| Ítem | Categoría | Desenlace | Detalle |\n");
        texto.Append("| ---- | --------- | --------- | ------- |\n");

        foreach (var resultado in Resultados)
        {
            texto.Append(CultureInfo.InvariantCulture,
                $"| `{resultado.Id}` | `{resultado.Categoria}` | {Nombrar(resultado.Desenlace)} "
                + $"| {resultado.Detalle} |\n");
        }
    }

    private static string Nombrar(DesenlaceDeItem desenlace) => desenlace switch
    {
        DesenlaceDeItem.TraduccionCorrecta => "Traducción correcta",
        DesenlaceDeItem.TraduccionIncorrecta => "Traducción incorrecta",
        DesenlaceDeItem.AbstencionSobreloFactible => "Se abstuvo ante algo contestable",
        DesenlaceDeItem.AbstencionCorrecta => "Abstención correcta",
        DesenlaceDeItem.IntentoSobreLoInfactible => "Intentó responder lo infactible",
        DesenlaceDeItem.Fallo => "Falló el turno",
        DesenlaceDeItem.GeneracionTruncada => "Generación truncada por presupuesto",
        _ => desenlace.ToString(),
    };
}
