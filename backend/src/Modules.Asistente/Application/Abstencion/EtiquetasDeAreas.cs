namespace Modules.Asistente.Application;

/// <summary>
/// Traduce las tablas del catálogo de capacidades a etiquetas humanas, para nombrar las áreas
/// consultables en un rechazo (design.md D4 de asistente-rechazos-dinamicos).
/// </summary>
internal static class EtiquetasDeAreas
{
    /// <summary>Cuántas etiquetas como máximo se listan antes del «, entre otros datos».</summary>
    private const int MaximoDeEtiquetas = 5;

    /// <summary>
    /// El mapa tabla calificada → etiqueta, EN EL ORDEN DECLARADO. Es una lista
    /// y no un diccionario porque el orden es observable: es el orden en que se
    /// listan las áreas cuando el actor cubre varias.
    /// </summary>
    private static readonly IReadOnlyList<(string Tabla, string Etiqueta)> Mapa =
    [
        ("designaciones.designaciones", "designaciones"),
        ("designaciones.pedidos", "pedidos de designación"),
        ("designaciones.pedido_historial", "pedidos de designación"),
        ("designaciones.pedido_adjuntos", "pedidos de designación"),
        ("identity.materias", "materias"),
        ("identity.planes", "materias"),
        ("identity.materias_plan", "materias"),
        ("identity.carreras", "carreras"),
        ("designaciones.cargos", "cargos"),
        ("designaciones.dedicaciones", "cargos"),
        ("designaciones.periodos", "períodos"),
        ("identity.personas", "docentes"),
        ("portal.perfiles", "perfiles del portal docente"),
        ("portal.educaciones", "perfiles del portal docente"),
        ("portal.certificaciones", "perfiles del portal docente"),
        ("portal.experiencias", "perfiles del portal docente"),
        ("portal.habilidades", "perfiles del portal docente"),
        ("portal.docente_habilidades", "perfiles del portal docente"),
        ("identity.users", "usuarios con sus roles"),
        ("identity.roles", "usuarios con sus roles"),
        ("identity.user_roles", "usuarios con sus roles"),
        ("identity.permisos", "usuarios con sus roles"),
        ("identity.rol_permisos", "usuarios con sus roles"),
    ];

    /// <summary>
    /// Los schemas que el asistente tiene habilitados: los de las tablas con etiqueta.
    /// </summary>
    internal static readonly IReadOnlySet<string> SchemasHabilitados =
        Mapa.Select(par => par.Tabla[..par.Tabla.IndexOf('.', StringComparison.Ordinal)])
            .ToHashSet(StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> TablasConocidas =
        Mapa.Select(par => par.Tabla).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Las etiquetas de las áreas que <paramref name="cubre"/> nombra, en el orden declarado, sin
    /// repetir, hasta cinco, con «, entre otros datos» cuando hay más — o <c>null</c> si ninguna
    /// tabla cubierta tiene etiqueta.
    /// </summary>
    public static string? Nombrar(IReadOnlyList<AreaCubierta> cubre)
    {
        ArgumentNullException.ThrowIfNull(cubre);

        var cubiertas = cubre
            .Select(area => area.Nombre)
            .Where(TablasConocidas.Contains)
            .ToHashSet(StringComparer.Ordinal);

        var etiquetas = Mapa
            .Where(par => cubiertas.Contains(par.Tabla))
            .Select(par => par.Etiqueta)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (etiquetas.Count == 0)
        {
            return null;
        }

        var texto = string.Join(", ", etiquetas.Take(MaximoDeEtiquetas));

        return etiquetas.Count > MaximoDeEtiquetas ? $"{texto}, entre otros datos" : texto;
    }

    /// <summary>Si esta tabla calificada tiene una etiqueta declarada.</summary>
    internal static bool Conoce(string tablaCalificada) => TablasConocidas.Contains(tablaCalificada);
}
