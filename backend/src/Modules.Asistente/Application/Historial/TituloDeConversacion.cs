namespace Modules.Asistente.Application;

/// <summary>
/// Deriva el título automático de una conversación nueva a partir de su
/// primera pregunta.
/// </summary>
/// <remarks>
/// La conversación se auto-titula (asistente-historial-conversaciones); un
/// usuario puede sobreescribir este título renombrándola, y esa renombrada
/// nunca se pisa por un turno posterior — este helper sólo se llama la
/// primera vez que una conversación se persiste.
/// </remarks>
internal static class TituloDeConversacion
{
    /// <summary>Longitud máxima del título, antes de la elipsis.</summary>
    public const int LongitudMaxima = 80;

    /// <summary>Deriva el título a partir de la primera pregunta.</summary>
    public static string Derivar(string primeraPregunta)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(primeraPregunta);

        var recortada = primeraPregunta.Trim();

        if (recortada.Length <= LongitudMaxima)
        {
            return recortada;
        }

        // Se trunca en el último espacio antes del límite, para no cortar una
        // palabra al medio. Si no hay ningún espacio en ese tramo —una sola
        // palabra larguísima—, se corta a lo bruto: no hay mejor opción.
        var corte = recortada[..LongitudMaxima];
        var ultimoEspacio = corte.LastIndexOf(' ');

        var texto = ultimoEspacio > 0 ? corte[..ultimoEspacio] : corte;

        return texto.TrimEnd() + "…";
    }
}
