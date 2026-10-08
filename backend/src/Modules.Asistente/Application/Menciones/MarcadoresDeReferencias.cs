using System.Text.RegularExpressions;

namespace Modules.Asistente.Application;

/// <summary>
/// Numera los marcadores <c>$refN</c> de las menciones nuevas de un turno,
/// continuando donde quedaron los que ya trae el segmento (design.md D11 de
/// asistente-rediseno-v3: «numerados después de los que ya trae el hilo»).
/// </summary>
/// <remarks>
/// <b>No hay un contador en <see cref="HiloConversacional"/>.</b> El máximo se
/// deriva de las mismas <c>consultasAnteriores</c> que ya viajan al generador
/// —el mismo criterio con que el resto del módulo evita un segundo lugar que
/// pueda desincronizarse del primero (ver los comentarios de
/// <see cref="HiloConversacional.ConsultasVigentes"/>)—. Un pivote de tema ya
/// vacía esa lista, así que la numeración vuelve a arrancar en 1 sin código
/// propio para detectarlo.
/// </remarks>
internal static class MarcadoresDeReferencias
{
    private static readonly Regex PatronDeMarcador = new(@"\$ref(\d+)", RegexOptions.Compiled);

    /// <summary>
    /// Asigna un marcador a cada referencia nueva, en el orden en que llegaron.
    /// </summary>
    /// <remarks>
    /// Devuelve tuplas y no un tipo propio a propósito: <c>Modules.Asistente.Contracts</c>
    /// está vacío y cada tipo público nuevo de <c>Application</c> es una promesa
    /// de estabilidad que hay que poder justificar (ver
    /// <c>ArquitecturaAsistenteTests.SuperficiePublicaDeApplication</c>). Esta
    /// clase es <c>internal</c>, así que una tupla no fuerza nada a ser público.
    /// </remarks>
    public static IReadOnlyList<(string Marcador, TipoDeMencion Tipo, ResultadoDeMencion Entidad)> Asignar(
        IReadOnlyList<(TipoDeMencion Tipo, ResultadoDeMencion Entidad)> nuevas,
        IReadOnlyList<string>? consultasAnteriores)
    {
        ArgumentNullException.ThrowIfNull(nuevas);

        if (nuevas.Count == 0)
        {
            return [];
        }

        var maximo = 0;
        if (consultasAnteriores is not null)
        {
            foreach (var consulta in consultasAnteriores)
            {
                foreach (Match coincidencia in PatronDeMarcador.Matches(consulta))
                {
                    var numero = int.Parse(
                        coincidencia.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                    maximo = Math.Max(maximo, numero);
                }
            }
        }

        var resueltas = new List<(string Marcador, TipoDeMencion Tipo, ResultadoDeMencion Entidad)>(nuevas.Count);
        for (var i = 0; i < nuevas.Count; i++)
        {
            var (tipo, entidad) = nuevas[i];
            resueltas.Add(($"$ref{maximo + i + 1}", tipo, entidad));
        }

        return resueltas;
    }

    /// <summary>
    /// Cómo se describe una entidad en el prompt: nombre y carrera para una
    /// materia, nombre para un docente. Nunca el identificador (design.md D11).
    /// </summary>
    public static string Etiqueta(TipoDeMencion tipo, ResultadoDeMencion entidad) => tipo switch
    {
        TipoDeMencion.Materia => $"{entidad.Nombre} ({entidad.Carrera})",
        _ => entidad.Nombre,
    };
}
