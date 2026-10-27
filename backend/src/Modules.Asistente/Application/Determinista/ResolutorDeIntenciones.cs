namespace Modules.Asistente.Application;

/// <summary>Una intención reconocida con todos sus slots resueltos.</summary>
public sealed record IntencionResuelta(
    Intencion Intencion,
    string Destino,
    IReadOnlyDictionary<string, ValorDeSlot> Slots);

/// <summary>
/// Reconoce intenciones del catálogo cerrado y resuelve sus slots contra la base.
/// </summary>
public sealed class ResolutorDeIntenciones(
    CatalogoDeIntenciones catalogo, ICatalogoDelDominio dominio)
{
    /// <summary>
    /// La primera intención cuyos términos están todos y cuyos slots resuelven todos, o nulo.
    /// </summary>
    public async Task<IntencionResuelta?> ResolverAsync(string pregunta, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pregunta);

        var terminos = NormalizadorLexico.Terminos(pregunta);
        var candidatas = catalogo.Intenciones
            .Where(i => i.Terminos.IsSubsetOf(terminos) && !i.Excluye.Overlaps(terminos))
            .ToList();

        if (candidatas.Count == 0)
        {
            return null;
        }

        // El catálogo se consulta recién acá, y no antes de mirar los términos: sin
        // ninguna candidata no hay nada que resolver, y esta es la única línea del
        // resolutor que puede tocar la base.
        var valores = await dominio.ObtenerAsync(ct);

        // Enmarcada con espacios en las puntas para que el término se busque entero: sin los
        // marcos, «datos» encontraría a «bases de datos» por adentro.
        var enmarcada = $" {string.Join(' ', NormalizadorLexico.Palabras(pregunta))} ";

        foreach (var intencion in candidatas)
        {
            var resuelta = Resolver(intencion, enmarcada, valores);

            if (resuelta is not null)
            {
                return resuelta;
            }
        }

        return null;
    }

    private static IntencionResuelta? Resolver(
        Intencion intencion, string enmarcada, CatalogoDelDominio valores)
    {
        var resueltos = new Dictionary<string, ValorDeSlot>(StringComparer.Ordinal);

        foreach (var slot in intencion.Slots)
        {
            var mencionados = Mencionados(slot.Clase, enmarcada, valores);

            // Ni cero ni dos. Cero es que la pregunta no nombra nada de esa clase; dos es que
            // nombra dos cosas distintas y elegir una sería adivinar.
            if (mencionados.Count != 1 || mencionados[0] is null)
            {
                return null;
            }

            resueltos[slot.Nombre] = mencionados[0]!;
        }

        return new IntencionResuelta(intencion, intencion.Destino, resueltos);
    }

    /// <summary>
    /// Los valores de esa clase que la pregunta nombra, uno por término mencionado.
    /// </summary>
    private static List<ValorDeSlot?> Mencionados(
        ClaseDeSlot clase, string enmarcada, CatalogoDelDominio valores)
    {
        var encontrados = new List<ValorDeSlot?>();
        var cubiertos = new List<string>();

        foreach (var termino in valores.TerminosDe(clase))
        {
            if (!enmarcada.Contains($" {termino} ", StringComparison.Ordinal))
            {
                continue;
            }

            if (cubiertos.Any(largo =>
                largo.Contains($" {termino} ", StringComparison.Ordinal)
                || largo.StartsWith($"{termino} ", StringComparison.Ordinal)
                || largo.EndsWith($" {termino}", StringComparison.Ordinal)))
            {
                continue;
            }

            cubiertos.Add(termino);
            encontrados.Add(valores.Unico(clase, termino));
        }

        return encontrados;
    }
}
