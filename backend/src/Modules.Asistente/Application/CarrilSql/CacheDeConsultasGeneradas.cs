using System.Globalization;
using System.Text.RegularExpressions;

namespace Modules.Asistente.Application;

/// <summary>
/// Las consultas ya generadas para preguntas sin contexto
/// (asistente-optimizaciones-modelo-local, design.md D6).
/// </summary>
/// <remarks>
/// <b>Guarda la consulta, nunca filas.</b> Un acierto se vuelve a validar y a
/// ejecutar bajo el alcance de quien pregunta: RLS y GRANT deciden igual que
/// siempre, y la consulta no trae filtros de alcance porque las instrucciones de
/// la generación los prohíben. Lo que se ahorra es la llamada de generación —la
/// más cara del turno—, no ninguna decisión de permisos.
///
/// La clave lleva la variante de rol —el prefijo de esquema es otro con datos
/// personales— y la fecha de referencia, porque «este año» o «vigentes hoy» se
/// generan con ella.
///
/// Por proceso y acotada: al pasar el tope se desaloja la entrada más vieja. Es
/// singleton; quien la consume es el generador, que es scoped.
/// </remarks>
public sealed partial class CacheDeConsultasGeneradas(TimeProvider reloj)
{
    /// <summary>Cuántas entradas guarda como mucho.</summary>
    public const int Tope = 500;

    private readonly Lock _candado = new();
    private readonly Dictionary<string, (GeneracionDeSql Generacion, DateTimeOffset Vence, long Orden)> _entradas =
        new(StringComparer.Ordinal);
    private long _orden;

    /// <summary>Cuántas entradas hay ahora (vigentes o no).</summary>
    public int Cantidad
    {
        get
        {
            lock (_candado)
            {
                return _entradas.Count;
            }
        }
    }

    internal GeneracionDeSql? Buscar(string pregunta, bool conDatosPersonales, DateOnly hoy)
    {
        var clave = Clave(pregunta, conDatosPersonales, hoy);

        lock (_candado)
        {
            if (!_entradas.TryGetValue(clave, out var entrada))
            {
                return null;
            }

            if (entrada.Vence <= reloj.GetUtcNow())
            {
                _entradas.Remove(clave);
                return null;
            }

            return entrada.Generacion;
        }
    }

    internal void Guardar(
        string pregunta, bool conDatosPersonales, DateOnly hoy, GeneracionDeSql generacion, TimeSpan vigencia)
    {
        var clave = Clave(pregunta, conDatosPersonales, hoy);

        lock (_candado)
        {
            if (!_entradas.ContainsKey(clave) && _entradas.Count >= Tope)
            {
                var masVieja = _entradas.MinBy(par => par.Value.Orden).Key;
                _entradas.Remove(masVieja);
            }

            _entradas[clave] = (generacion, reloj.GetUtcNow() + vigencia, ++_orden);
        }
    }

    /// <summary>
    /// La pregunta normalizada: minúsculas, espacios colapsados y sin los signos
    /// de pregunta de los extremos. No se quitan tildes: «Díaz» y «Diaz» pueden
    /// ser dos personas distintas.
    /// </summary>
    internal static string Normalizar(string pregunta) =>
        Espacios().Replace(pregunta.Trim().Trim('¿', '?').Trim(), " ").ToLowerInvariant();

    private static string Clave(string pregunta, bool conDatosPersonales, DateOnly hoy) =>
        $"{(conDatosPersonales ? "pii" : "basico")}|{hoy.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}|{Normalizar(pregunta)}";

    [GeneratedRegex(@"\s+")]
    private static partial Regex Espacios();
}
