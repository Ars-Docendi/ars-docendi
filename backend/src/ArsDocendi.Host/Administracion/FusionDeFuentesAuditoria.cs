namespace ArsDocendi.Host.Administracion;

/// <summary>
/// La fusión de las dos fuentes del feed unificado de auditoría en el orden
/// total de design.md D2: <c>CambiadoEn DESC</c>, luego «cambios» antes que
/// «asistente» en un empate exacto de instante, luego <c>Id DESC</c>.
/// </summary>
/// <remarks>
/// Función pura y estática a propósito (tarea 2.5): sin ella, la corrección de
/// la fusión sólo se puede probar contra una base real, con datos que nunca
/// cubren el caso límite —dos fuentes con el MISMO <c>CambiadoEn</c>— que es
/// justo el que un property test necesita generar a voluntad.
///
/// Implementación: mezcla de dos secuencias YA ordenadas por el mismo orden
/// total (mergesort de dos vías), no la optimización de ventana con offset que
/// describe design.md D2 — <see cref="ServicioAuditoria"/> decide cuánto pedir
/// de cada fuente antes de llamar acá; esta función sólo responde "en qué
/// orden quedan, dadas las filas que ya trajo cada una" y es correcta para
/// cualquier par de listas de entrada, sea cual sea su tamaño.
/// </remarks>
public static class FusionDeFuentesAuditoria
{
    public const string OrigenCambios = "cambios";
    public const string OrigenAsistente = "asistente";

    /// <summary>Una fila, reducida a lo que el orden total necesita comparar.</summary>
    public sealed record Marca(string Origen, DateTimeOffset CambiadoEn, long Id);

    /// <summary>
    /// Fusiona <paramref name="cambios"/> y <paramref name="asistente"/>
    /// —cada una YA ordenada <c>CambiadoEn DESC, Id DESC</c>— y devuelve las
    /// filas <c>[offset, offset + tamano)</c> del orden total combinado.
    /// </summary>
    public static IReadOnlyList<Marca> FusionarVentana(
        IReadOnlyList<Marca> cambios, IReadOnlyList<Marca> asistente, int offset, int tamano)
    {
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        if (tamano < 0) throw new ArgumentOutOfRangeException(nameof(tamano));

        return [.. Fusionar(cambios, asistente).Skip(offset).Take(tamano)];
    }

    /// <summary>El orden total completo de las dos fuentes, sin recortar.</summary>
    public static IReadOnlyList<Marca> Fusionar(IReadOnlyList<Marca> cambios, IReadOnlyList<Marca> asistente)
    {
        var resultado = new List<Marca>(cambios.Count + asistente.Count);
        int i = 0, j = 0;
        while (i < cambios.Count && j < asistente.Count)
        {
            resultado.Add(Precede(cambios[i], asistente[j]) ? cambios[i++] : asistente[j++]);
        }

        while (i < cambios.Count) resultado.Add(cambios[i++]);
        while (j < asistente.Count) resultado.Add(asistente[j++]);

        return resultado;
    }

    /// <summary><c>true</c> si <paramref name="x"/> va ANTES que <paramref name="y"/> en el orden total.</summary>
    private static bool Precede(Marca x, Marca y)
    {
        if (x.CambiadoEn != y.CambiadoEn) return x.CambiadoEn > y.CambiadoEn;
        if (x.Origen != y.Origen) return x.Origen == OrigenCambios;
        return x.Id > y.Id;
    }
}
