namespace Modules.Asistente.Application;

/// <summary>
/// Una aparición, dentro del texto de la respuesta, de un valor que el resultado ya
/// vinculó.
/// </summary>
/// <param name="Desde">
/// Índice del primer carácter, como índice de <see cref="string"/> de .NET: unidades
/// de código UTF-16, las mismas que usa <c>String.prototype.slice</c> en el cliente.
/// </param>
/// <param name="Largo">Cuántas unidades de código ocupa el valor.</param>
/// <param name="Tipo">El tipo del vínculo de la celda de la que salió el valor.</param>
/// <param name="Id">El identificador del vínculo de esa celda.</param>
public sealed record VinculoDelTexto(int Desde, int Largo, string Tipo, string Id);

/// <summary>
/// Ubica en el texto final de la respuesta los valores de celda que tienen vínculo.
/// </summary>
/// <remarks>
/// <b>No resuelve nada y no sabe qué forma tiene un identificador.</b> Parte de los
/// vínculos que el resultado ya tiene (ver <see cref="BuscadorDeVinculos"/>) y busca
/// el valor de cada una de esas celdas en la prosa. Un número que aparece sólo en el
/// texto, o cuya celda no se resolvió, no se señala: el texto lo escribe el modelo, y
/// ninguna pantalla se abre por algo que el modelo dijo.
///
/// <b>La coincidencia es exacta, ordinal y por palabra completa.</b> Inmediatamente
/// antes y después del valor no puede haber una letra, un dígito ni un guion, para que
/// «2026-90051» no enlace «2026-9005» ni «x-2026-9005» lo haga. Sin normalizar
/// mayúsculas ni tildes: el vínculo se ofrece sobre el valor tal como lo devolvió la
/// consulta.
///
/// <b>Es una función pura.</b> Se llama con el texto que el usuario efectivamente va
/// a leer —después de cualquier reescritura del rechazo—, porque unas posiciones
/// calculadas sobre otro texto señalarían cualquier cosa.
/// </remarks>
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

    /// <summary>
    /// Si el tramo no está pegado a una letra, un dígito ni un guion.
    /// </summary>
    private static bool EsPalabraCompleta(string texto, int desde, int largo)
    {
        var fin = desde + largo;

        return (desde == 0 || !PerteneceALaPalabra(texto[desde - 1]))
            && (fin == texto.Length || !PerteneceALaPalabra(texto[fin]));
    }

    private static bool PerteneceALaPalabra(char caracter) =>
        char.IsLetterOrDigit(caracter) || caracter == '-';

    /// <summary>
    /// En orden de aparición y sin superposiciones.
    /// </summary>
    /// <remarks>
    /// Dos valores distintos pueden compartir tramo si contienen signos que no cuentan
    /// como palabra («a/b» y «b»). El cliente pinta segmentos consecutivos, así que una
    /// superposición lo obligaría a decidir; se resuelve acá: gana el que empieza antes
    /// y, a igual inicio, el más largo.
    /// </remarks>
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
