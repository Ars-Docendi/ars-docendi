namespace Modules.Asistente.Application;

/// <summary>
/// De las filas de un resultado a los candidatos a vínculo, y de los candidatos
/// resueltos a los vínculos ubicados.
/// </summary>
/// <remarks>
/// <b>Los candidatos salen de los VALORES, no de la procedencia de la columna.</b>
/// Hay una forma más exacta: PostgreSQL reporta por columna el identificador de la
/// tabla y el número de atributo, y el enmascarador ya los usa para clasificar
/// sensibilidad sin depender del alias. Con eso se sabría con certeza que una
/// columna <b>es</b> el número de trámite.
///
/// No se usa por lo que el propio enmascarador tiene medido: ese par se pierde en
/// cuanto la consulta deja de ser una proyección directa. <c>DISTINCT</c>,
/// <c>GROUP BY</c>, <c>UNION</c> y cualquier expresión reportan cero —está escrito
/// en <see cref="ClasificacionDeSensibilidad.Desconocida"/> y registrado como
/// TD-009—, y el modelo escribe <c>DISTINCT</c> seguido. El vínculo desaparecería
/// justo en las consultas que listan varios trámites.
///
/// <b>La objeción obvia se responde sola.</b> «Un texto cualquiera podría coincidir
/// con el identificador de otra cosa»: sí, y lo valida el dueño del recurso contra
/// una columna única. Una coincidencia exacta de celda completa <b>es</b> ese
/// recurso; y si el actor no lo alcanza, tampoco hay vínculo.
///
/// <b>Acá no se sabe qué forma tiene un identificador.</b> El asistente no conoce
/// la numeración de ningún módulo, así que el filtro es genérico —texto corto, sin
/// espacios— y descartar lo que no corresponde es del que resuelve.
/// </remarks>
public static class BuscadorDeVinculos
{
    /// <summary>
    /// Largo máximo de un candidato.
    /// </summary>
    /// <remarks>
    /// Un identificador legible es corto. El tope existe para que una columna de
    /// texto libre —una justificación, un comentario— no viaje entera como
    /// candidato: no va a resolver nunca y sólo engorda el pedido.
    /// </remarks>
    internal const int LargoMaximoDelCandidato = 40;

    /// <summary>
    /// Cuántos candidatos distintos se proponen como mucho.
    /// </summary>
    /// <remarks>
    /// El resultado ya viene recortado al tope de filas, así que en la práctica no
    /// se llega. Está para que subir ese tope no convierta esto en un pedido
    /// arbitrariamente grande sin que nadie lo decida.
    /// </remarks>
    internal const int TopeDeCandidatos = 200;

    /// <summary>
    /// Los valores de celda que podrían ser el identificador de algo.
    /// </summary>
    public static IReadOnlyList<string> Candidatos(IReadOnlyList<IReadOnlyList<object?>> filas)
    {
        ArgumentNullException.ThrowIfNull(filas);

        var vistos = new HashSet<string>(StringComparer.Ordinal);

        foreach (var fila in filas)
        {
            foreach (var valor in fila)
            {
                if (EsCandidato(valor, out var texto) && vistos.Count < TopeDeCandidatos)
                {
                    vistos.Add(texto);
                }
            }
        }

        return [.. vistos];
    }

    /// <summary>
    /// Ubica en el resultado cada candidato que resolvió.
    /// </summary>
    /// <remarks>
    /// Un mismo identificador puede aparecer en más de una celda —dos filas del
    /// historial del mismo trámite, por ejemplo—, y cada una lleva su vínculo: la
    /// celda es lo que el usuario aprieta, así que la que quede sin él parecería
    /// rota.
    /// </remarks>
    public static IReadOnlyList<VinculoDelResultado> Ubicar(
        IReadOnlyList<IReadOnlyList<object?>> filas,
        IReadOnlyDictionary<string, DestinoDelVinculo> destinos)
    {
        ArgumentNullException.ThrowIfNull(filas);
        ArgumentNullException.ThrowIfNull(destinos);

        if (destinos.Count == 0)
        {
            return [];
        }

        var vinculos = new List<VinculoDelResultado>();

        for (var fila = 0; fila < filas.Count; fila++)
        {
            var celdas = filas[fila];

            for (var columna = 0; columna < celdas.Count; columna++)
            {
                if (EsCandidato(celdas[columna], out var texto)
                    && destinos.TryGetValue(texto, out var destino))
                {
                    vinculos.Add(
                        new VinculoDelResultado(fila, columna, destino.Tipo, destino.Id));
                }
            }
        }

        return vinculos;
    }

    /// <summary>
    /// Si el valor de una celda tiene forma de identificador.
    /// </summary>
    /// <remarks>
    /// <b>Sólo cadenas.</b> Un número o una fecha no identifican un recurso de este
    /// sistema, y aceptarlos haría que cualquier columna de conteo propusiera
    /// candidatos.
    ///
    /// <b>Sin espacios.</b> Es lo que separa un identificador de una oración, y
    /// además garantiza que el valor de la celda sea el identificador COMPLETO: un
    /// texto que sólo <i>menciona</i> un número —«ver el 2026-9001»— no coincide con
    /// ninguno, así que no puede producir un vínculo falso.
    /// </remarks>
    private static bool EsCandidato(object? valor, out string texto)
    {
        texto = string.Empty;

        if (valor is not string crudo)
        {
            return false;
        }

        if (crudo.Length is 0 or > LargoMaximoDelCandidato)
        {
            return false;
        }

        if (crudo.Any(char.IsWhiteSpace))
        {
            return false;
        }

        texto = crudo;
        return true;
    }
}
