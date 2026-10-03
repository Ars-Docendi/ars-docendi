namespace Modules.Asistente.Application;

/// <summary>
/// Reescribe los marcadores <c>$refN</c> de una consulta ya validada a
/// parámetros con nombre, para que el ejecutor los ligue como <c>uuid</c> en vez
/// de interpolarlos (design.md D11 de asistente-rediseno-v3).
/// </summary>
/// <remarks>
/// <b>Reemplaza sólo el <c>$</c> por un <c>@</c>, carácter por carácter, en el
/// mismo texto.</b> No reconstruye la consulta a partir de los tokens: el
/// tokenizador es un escáner que EMITE lo que el validador necesita ver y
/// descarta el resto —operadores, paréntesis, espacios—, así que la lista de
/// tokens no alcanza para reconstruir el SQL completo. Un marcador y su
/// reemplazo tienen el mismo largo —<c>$ref12</c> y <c>@ref12</c> son las mismas
/// seis posiciones—, así que la única cirugía segura es tocar un carácter en su
/// lugar, nunca recortar ni reinsertar texto.
///
/// Corre DESPUÉS del validador, sobre la MISMA consulta que ya pasó por
/// <see cref="ValidadorDeSql.Validar"/>: si un marcador apareciera acá que no
/// estuviera en <paramref name="bindings"/>… ver la nota de
/// <see cref="Reescribir"/>.
/// </remarks>
internal static class ReescritorDeMarcadores
{
    /// <param name="sql">La consulta generada, ya validada.</param>
    /// <param name="bindings">
    /// El identificador de cada marcador declarado para el turno —los nuevos y
    /// los heredados del segmento—, con el <c>$</c> incluido en la clave
    /// (<c>"$ref1"</c>). El validador ya garantizó que todo marcador que
    /// aparezca en <paramref name="sql"/> está acá.
    /// </param>
    /// <returns>
    /// La consulta con cada <c>$refN</c> convertido en <c>@refN</c>, y los
    /// bindings —sin el <c>$</c>, listos para nombrar un <see cref="Npgsql.NpgsqlParameter"/>—
    /// que de verdad se usaron. Un marcador declarado que la consulta no
    /// mencionó no aparece acá: agregarle un parámetro que ningún <c>@refN</c>
    /// nombra no rompe nada en Npgsql, pero sería binding muerto que nadie pidió.
    /// </returns>
    public static (string Sql, IReadOnlyDictionary<string, Guid> Bindings) Reescribir(
        string sql, IReadOnlyDictionary<string, Guid> bindings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentNullException.ThrowIfNull(bindings);

        var tokens = TokenizadorSql.Tokenizar(sql);
        var marcadores = tokens.Where(t => t.Clase == ClaseDeToken.Marcador).ToArray();

        if (marcadores.Length == 0)
        {
            return (sql, new Dictionary<string, Guid>());
        }

        var caracteres = sql.ToCharArray();
        var usados = new Dictionary<string, Guid>(StringComparer.Ordinal);

        foreach (var token in marcadores)
        {
            caracteres[token.Inicio] = '@';
            var nombre = token.Texto[1..];

            if (usados.ContainsKey(nombre))
            {
                continue;
            }

            if (!bindings.TryGetValue(token.Texto, out var id))
            {
                // El validador ya exigió que todo marcador usado esté declarado
                // (VerificarMarcadores), y `bindings` trae exactamente los
                // declarados: llegar acá es un error de plomería entre el
                // validador y el ejecutor —dos listas de marcadores que
                // divergieron—, no un dato que haya mandado el usuario ni el
                // modelo. Por eso revienta en vez de abstenerse: abstenerse
                // esconde el bug detrás de una respuesta que parece normal.
                throw new InvalidOperationException(
                    $"El marcador '{token.Texto}' no tiene binding. El validador debería "
                    + "haber rechazado esta consulta antes de llegar al ejecutor.");
            }

            usados[nombre] = id;
        }

        return (new string(caracteres), usados);
    }
}
