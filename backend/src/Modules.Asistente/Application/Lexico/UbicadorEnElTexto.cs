namespace Modules.Asistente.Application;

/// <summary>
/// Una aparición, dentro del texto de la respuesta, de un valor que el resultado ya vinculó.
/// </summary>
public sealed record VinculoDelTexto(int Desde, int Largo, string Tipo, string Id);

/// <summary>
/// Ubica en el texto final de la respuesta los valores de celda que tienen vínculo.
/// </summary>
internal static class UbicadorEnElTexto
{
    public static IReadOnlyList<VinculoDelTexto> Ubicar(
        string texto,
        IReadOnlyList<IReadOnlyList<object?>> filas,
        IReadOnlyList<VinculoDelResultado> vinculos)
    {
        ArgumentNullException.ThrowIfNull(texto);
        ArgumentNullException.ThrowIfNull(filas);
        ArgumentNullException.ThrowIfNull(vinculos);

        if (texto.Length == 0 || vinculos.Count == 0)
        {
            return [];
        }

        // Un valor, un destino: dos celdas con el mismo valor son el mismo recurso,
        // porque el resolutor contesta por valor.
        var destinos = new Dictionary<string, VinculoDelResultado>(StringComparer.Ordinal);

        foreach (var vinculo in vinculos)
        {
            if (filas[vinculo.Fila][vinculo.Columna] is string { Length: > 0 } valor)
            {
                destinos.TryAdd(valor, vinculo);
            }
        }

        var encontrados = new List<VinculoDelTexto>();

        foreach (var (valor, vinculo) in destinos)
        {
            var desde = 0;

            while ((desde = texto.IndexOf(valor, desde, StringComparison.Ordinal)) >= 0)
            {
                if (EsPalabraCompleta(texto, desde, valor.Length))
                {
                    encontrados.Add(new VinculoDelTexto(desde, valor.Length, vinculo.Tipo, vinculo.Id));
                }

                desde += valor.Length;
            }
        }

        return SinSolapar(encontrados);
    }

    /// <summary>Si el tramo no está pegado a una letra, un dígito ni un guion.</summary>
    private static bool EsPalabraCompleta(string texto, int desde, int largo)
    {
        var fin = desde + largo;

        return (desde == 0 || !PerteneceALaPalabra(texto[desde - 1]))
            && (fin == texto.Length || !PerteneceALaPalabra(texto[fin]));
    }

    private static bool PerteneceALaPalabra(char caracter) =>
        char.IsLetterOrDigit(caracter) || caracter == '-';

    /// <summary>En orden de aparición y sin superposiciones.</summary>
    private static IReadOnlyList<VinculoDelTexto> SinSolapar(List<VinculoDelTexto> encontrados)
    {
        var resultado = new List<VinculoDelTexto>(encontrados.Count);
        var hasta = 0;

        foreach (var aparicion in encontrados.OrderBy(a => a.Desde).ThenByDescending(a => a.Largo))
        {
            if (aparicion.Desde >= hasta)
            {
                resultado.Add(aparicion);
                hasta = aparicion.Desde + aparicion.Largo;
            }
        }

        return resultado;
    }
}
