namespace ArsDocendi.IntegrationTests.Backend;

/// <summary>Un ciclo, enumerado en el orden en que se recorre.</summary>
public sealed record CicloDeProyectos(IReadOnlyList<string> Proyectos)
{
    public override string ToString() => string.Join(" -> ", Proyectos);
}

/// <summary>
/// Comprueba el invariante #2 —el grafo de proyectos es acíclico— sobre las aristas
/// leídas de los <c>.csproj</c>.
/// </summary>
/// <remarks>
/// Corre sobre el código y no sobre lo declarado a propósito. Con las tres
/// direcciones del comparador en verde los dos conjuntos son idénticos y la
/// elección parece indiferente; deja de serlo justo cuando algo está roto: si el
/// manifiesto quedó desactualizado, lo que interesa saber es si el CÓDIGO tiene un
/// ciclo, y afirmarlo sobre el papel respondería sobre un grafo que ya no existe.
///
/// Hasta este cambio el invariante #2 se cumplía por inspección humana: ningún test
/// construía el grafo.
/// </remarks>
public static class DetectorDeCiclos
{
    public static IReadOnlyList<CicloDeProyectos> Detectar(GrafoDeProyectos grafo)
    {
        var salientes = grafo.Aristas
            .GroupBy(a => a.Origen, StringComparer.Ordinal)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => grupo.Select(a => a.Destino).ToArray(),
                StringComparer.Ordinal);

        var terminados = new HashSet<string>(StringComparer.Ordinal);
        var camino = new List<string>();
        var ciclos = new List<CicloDeProyectos>();

        foreach (var proyecto in grafo.Proyectos.OrderBy(n => n, StringComparer.Ordinal))
        {
            Recorrer(proyecto);
        }

        return ciclos;

        void Recorrer(string nodo)
        {
            if (terminados.Contains(nodo))
            {
                return;
            }

            // `camino` es la pila de nodos EN CURSO. Encontrar el nodo ahí adentro es
            // lo único que distingue un ciclo de un rombo: sin esta comprobación, un
            // recorrido que solo marca lo ya visitado no detecta ningún ciclo y pasa
            // en verde para siempre.
            var posicion = camino.IndexOf(nodo);
            if (posicion >= 0)
            {
                ciclos.Add(new CicloDeProyectos([.. camino[posicion..], nodo]));
                return;
            }

            camino.Add(nodo);

            if (salientes.TryGetValue(nodo, out var destinos))
            {
                foreach (var destino in destinos.OrderBy(n => n, StringComparer.Ordinal))
                {
                    Recorrer(destino);
                }
            }

            camino.RemoveAt(camino.Count - 1);
            terminados.Add(nodo);
        }
    }
}
