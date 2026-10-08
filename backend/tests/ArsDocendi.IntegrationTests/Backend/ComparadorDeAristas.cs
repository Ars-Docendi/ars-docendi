namespace ArsDocendi.IntegrationTests.Backend;

/// <summary>Las formas en que el manifiesto y el código pueden dejar de coincidir.</summary>
public enum TipoDesviacionDeArista
{
    /// <summary>Dirección 1: un <c>.csproj</c> referencia algo que el manifiesto no declara.</summary>
    AristaNoDeclarada,

    /// <summary>Dirección 2: el manifiesto declara una arista que ningún <c>.csproj</c> tiene.</summary>
    AristaDeclaradaInexistente,

    /// <summary>Dirección 3: existe un proyecto que el manifiesto no enumera.</summary>
    ProyectoSinClasificar,

    /// <summary>Dirección 3: el manifiesto enumera un proyecto que ya no existe.</summary>
    ProyectoDeclaradoInexistente,

    /// <summary>El estado declarado no coincide con si alguna arista real alcanza al proyecto.</summary>
    EstadoDeProyectoIncoherente,

    /// <summary>Un proyecto huérfano sin el motivo escrito que explica por qué se lo conserva.</summary>
    HuerfanoSinMotivo,

    /// <summary>Una arista declarada como excepción sin el ticket que la aprobó.</summary>
    ExcepcionSinTicket,

    /// <summary>Una arista declarada como excepción sin motivo escrito.</summary>
    ExcepcionSinMotivo,

    /// <summary>Una arista declarada como excepción sin decir a qué invariante excede.</summary>
    ExcepcionSinInvariante,
}

/// <summary>Una desviación entre lo declarado y el código, con el objeto nombrado.</summary>
public sealed record DesviacionDeArista(TipoDesviacionDeArista Tipo, string Objeto, string Detalle)
{
    public override string ToString() => $"[{Tipo}] {Objeto} — {Detalle}";
}

/// <summary>
/// Compara el manifiesto contra el grafo real en las TRES direcciones.
/// </summary>
/// <remarks>
/// Verificar una sola deja las otras dos abiertas: una arista en el código sin fila
/// es una frontera que nadie decidió; una fila sin arista es un registro que miente
/// y dejó de proteger; y un proyecto sin clasificar es la puerta por la que entró
/// <c>Modules.Asistente.Contracts</c>, que hoy vive explicado en un párrafo.
///
/// La tercera es la que heredamos del manifiesto de privilegios, donde atrapó
/// <c>__EFMigrationsHistory</c> en su primera corrida.
///
/// Devuelve desviaciones tipadas y no un booleano: un test que solo sabe que «algo
/// no coincide» obliga a reconstruir a mano qué, y lo que se necesita saber en el
/// CI es exactamente qué arista y en qué dirección.
/// </remarks>
public static class ComparadorDeAristas
{
    public static IReadOnlyList<DesviacionDeArista> Comparar(
        ManifiestoDeAristas manifiesto, GrafoDeProyectos grafo)
    {
        var desviaciones = new List<DesviacionDeArista>();

        var declaradas = manifiesto.Aristas
            .Select(a => (a.Origen, a.Destino))
            .ToHashSet();
        var reales = grafo.Aristas
            .Select(a => (a.Origen, a.Destino))
            .ToHashSet();

        // Dirección 1 — el código referencia algo que el manifiesto no declara.
        foreach (var arista in grafo.Aristas
            .OrderBy(a => a.Origen, StringComparer.Ordinal)
            .ThenBy(a => a.Destino, StringComparer.Ordinal))
        {
            if (!declaradas.Contains((arista.Origen, arista.Destino)))
            {
                desviaciones.Add(new DesviacionDeArista(
                    TipoDesviacionDeArista.AristaNoDeclarada,
                    arista.ToString(),
                    "el .csproj de origen la referencia y el manifiesto no tiene su fila"));
            }
        }

        // Dirección 2 — el manifiesto declara una arista que el código no tiene.
        foreach (var arista in manifiesto.Aristas
            .OrderBy(a => a.Origen, StringComparer.Ordinal)
            .ThenBy(a => a.Destino, StringComparer.Ordinal))
        {
            if (!reales.Contains((arista.Origen, arista.Destino)))
            {
                desviaciones.Add(new DesviacionDeArista(
                    TipoDesviacionDeArista.AristaDeclaradaInexistente,
                    arista.ToString(),
                    "el manifiesto la declara y ningún .csproj de backend/src la referencia"));
            }
        }

        desviaciones.AddRange(RevisarExcepciones(manifiesto));
        desviaciones.AddRange(CompararProyectos(manifiesto, grafo, reales));

        return desviaciones;
    }

    /// <summary>
    /// Una excepción a un invariante es una fila con invariante, ticket y motivo.
    /// </summary>
    /// <remarks>
    /// Sólo se revisa lo declarado excepción: si el comparador exigiera ticket a toda
    /// arista, las filas comunes estarían en rojo y la salida sería quitar el guard.
    ///
    /// Es la misma forma que <c>Toda_denegacion_explicita_lleva_motivo_escrito</c> en
    /// el manifiesto de privilegios: lo que se aparta de la regla lleva escrito por
    /// qué, y quién lo aprobó.
    /// </remarks>
    private static IEnumerable<DesviacionDeArista> RevisarExcepciones(ManifiestoDeAristas manifiesto)
    {
        foreach (var arista in manifiesto.Aristas.Where(a => a.EsExcepcion))
        {
            if (string.IsNullOrWhiteSpace(arista.Excepcion!.Ticket))
            {
                yield return new DesviacionDeArista(
                    TipoDesviacionDeArista.ExcepcionSinTicket,
                    arista.ToString(),
                    "está declarada excepción a un invariante y no indica el ticket que la aprobó");
            }

            if (string.IsNullOrWhiteSpace(arista.Excepcion!.Invariante))
            {
                yield return new DesviacionDeArista(
                    TipoDesviacionDeArista.ExcepcionSinInvariante,
                    arista.ToString(),
                    "está declarada excepción y no indica a qué invariante excede");
            }

            if (string.IsNullOrWhiteSpace(arista.Motivo))
            {
                yield return new DesviacionDeArista(
                    TipoDesviacionDeArista.ExcepcionSinMotivo,
                    arista.ToString(),
                    "está declarada excepción a un invariante y no tiene motivo escrito");
            }
        }
    }

    /// <summary>Dirección 3: todo proyecto real está clasificado, y todo clasificado existe.</summary>
    private static IEnumerable<DesviacionDeArista> CompararProyectos(
        ManifiestoDeAristas manifiesto,
        GrafoDeProyectos grafo,
        IReadOnlySet<(string Origen, string Destino)> reales)
    {
        var desviaciones = new List<DesviacionDeArista>();

        var declarados = manifiesto.Proyectos
            .Select(p => p.Nombre)
            .ToHashSet(StringComparer.Ordinal);
        var existentes = grafo.Proyectos.ToHashSet(StringComparer.Ordinal);

        // Un proyecto está «alcanzado» si alguna arista REAL lo nombra. Se mira el
        // código y no lo declarado a propósito: si el manifiesto quedó desactualizado,
        // lo que interesa es qué proyecto quedó suelto de verdad.
        var alcanzados = reales
            .SelectMany(a => new[] { a.Origen, a.Destino })
            .ToHashSet(StringComparer.Ordinal);

        foreach (var proyecto in grafo.Proyectos.OrderBy(n => n, StringComparer.Ordinal))
        {
            if (!declarados.Contains(proyecto))
            {
                desviaciones.Add(new DesviacionDeArista(
                    TipoDesviacionDeArista.ProyectoSinClasificar,
                    proyecto,
                    "existe bajo backend/src y el manifiesto no lo enumera"));
            }
        }

        foreach (var proyecto in manifiesto.Proyectos
            .OrderBy(p => p.Nombre, StringComparer.Ordinal))
        {
            if (!existentes.Contains(proyecto.Nombre))
            {
                desviaciones.Add(new DesviacionDeArista(
                    TipoDesviacionDeArista.ProyectoDeclaradoInexistente,
                    proyecto.Nombre,
                    "el manifiesto lo enumera y no hay ningún .csproj con ese nombre"));
                continue;
            }

            var alcanzado = alcanzados.Contains(proyecto.Nombre);

            if (alcanzado && proyecto.EsHuerfano)
            {
                desviaciones.Add(new DesviacionDeArista(
                    TipoDesviacionDeArista.EstadoDeProyectoIncoherente,
                    proyecto.Nombre,
                    "está declarado huerfano y alguna arista real lo alcanza"));
            }

            if (!alcanzado && !proyecto.EsHuerfano)
            {
                desviaciones.Add(new DesviacionDeArista(
                    TipoDesviacionDeArista.EstadoDeProyectoIncoherente,
                    proyecto.Nombre,
                    "ninguna arista real lo alcanza y no está declarado huerfano"));
            }

            if (proyecto.EsHuerfano && string.IsNullOrWhiteSpace(proyecto.Motivo))
            {
                desviaciones.Add(new DesviacionDeArista(
                    TipoDesviacionDeArista.HuerfanoSinMotivo,
                    proyecto.Nombre,
                    "está declarado huerfano sin el motivo escrito que explica por qué se lo conserva"));
            }
        }

        return desviaciones;
    }
}
