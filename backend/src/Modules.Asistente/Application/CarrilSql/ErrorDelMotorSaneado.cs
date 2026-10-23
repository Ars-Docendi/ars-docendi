using System.Text.RegularExpressions;

namespace Modules.Asistente.Application;

/// <summary>
/// El error del motor, en la forma en que puede viajar al modelo para la ronda de
/// reparación (asistente-optimizaciones-modelo-local, design.md D4).
/// </summary>
/// <remarks>
/// <b>La regla es una sola: un literal citado que no está en la consulta no
/// viaja.</b> Lo que está en la consulta lo escribió el modelo —nombres de
/// columnas, constantes de un filtro— y devolvérselo no le cuenta nada nuevo. Lo
/// que no está puede venir de una FILA: un cast que falla sobre el valor de una
/// columna lo cita entero. Ese valor no pasó por el enmascarador, y mandarlo al
/// proveedor sería sacar un dato por la puerta de atrás.
/// </remarks>
internal static partial class ErrorDelMotorSaneado
{
    /// <summary>Tope del texto que viaja: el motor puede ser verborrágico.</summary>
    private const int Tope = 300;

    /// <summary>El problema, listo para el bloque «Intento anterior».</summary>
    public static string Problema(string sql, string? estado, string? detalle)
    {
        ArgumentNullException.ThrowIfNull(sql);

        var saneado = string.IsNullOrWhiteSpace(detalle)
            ? "sin detalle"
            : Literal().Replace(detalle.Trim(), coincidencia =>
                sql.Contains(coincidencia.Groups["valor"].Value, StringComparison.OrdinalIgnoreCase)
                    ? coincidencia.Value
                    : $"{coincidencia.Groups["comilla"].Value}…{coincidencia.Groups["comilla"].Value}");

        if (saneado.Length > Tope)
        {
            saneado = saneado[..Tope] + "…";
        }

        return $"PostgreSQL rechazó la consulta (SQLSTATE {estado ?? "desconocido"}): {saneado}";
    }

    /// <summary>
    /// Si vale la pena pedir una corrección. Privilegio (42501) es la frontera
    /// funcionando, no un error de SQL; timeout de sentencia (57014) no se
    /// abarata corrigiendo de forma confiable.
    /// </summary>
    public static bool EsReparable(string? estado) => estado is not ("42501" or "57014");

    [GeneratedRegex("""(?<comilla>["'])(?<valor>[^"']*)\k<comilla>""")]
    private static partial Regex Literal();
}
