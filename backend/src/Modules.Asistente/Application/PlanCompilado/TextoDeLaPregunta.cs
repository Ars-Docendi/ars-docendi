using System.Globalization;

namespace Modules.Asistente.Application;

/// <summary>
/// La pregunta normalizada para las comprobaciones léxicas del plan: minúsculas,
/// sin acentos, en palabras.
/// </summary>
/// <remarks>
/// Todo lo que el plan compara contra la pregunta —la puerta, el anclaje, el
/// cierre— pasa por acá, y por eso las frases del catálogo se escriben también
/// sin acentos y en minúsculas. Las búsquedas son siempre por palabra entera: sin
/// eso, «adjunto» aparecería dentro de «adjuntos» como corresponde pero «tres»
/// aparecería dentro de «trescientos».
/// </remarks>
internal sealed class TextoDeLaPregunta
{
    private static readonly Dictionary<string, int> NumerosEnPalabras = new(StringComparer.Ordinal)
    {
        ["cero"] = 0, ["un"] = 1, ["una"] = 1, ["uno"] = 1, ["dos"] = 2, ["tres"] = 3,
        ["cuatro"] = 4, ["cinco"] = 5, ["seis"] = 6, ["siete"] = 7, ["ocho"] = 8,
        ["nueve"] = 9, ["diez"] = 10, ["once"] = 11, ["doce"] = 12, ["quince"] = 15,
        ["veinte"] = 20, ["veinticinco"] = 25, ["treinta"] = 30, ["cuarenta"] = 40,
        ["cincuenta"] = 50,
    };

    private readonly string _plano;

    public TextoDeLaPregunta(string pregunta)
    {
        ArgumentNullException.ThrowIfNull(pregunta);

        Original = pregunta;
        Palabras = NormalizadorLexico.Palabras(pregunta);
        _plano = $" {string.Join(' ', Palabras)} ";
    }

    public string Original { get; }

    public IReadOnlyList<string> Palabras { get; }

    /// <summary>Normaliza una frase del catálogo o un valor del plan igual que la pregunta.</summary>
    public static string Normalizar(string frase) =>
        string.Join(' ', NormalizadorLexico.Palabras(frase));

    /// <summary>Si la pregunta contiene la frase, como palabras enteras.</summary>
    public bool Contiene(string frase)
    {
        var normalizada = Normalizar(frase);
        return normalizada.Length > 0 && _plano.Contains($" {normalizada} ", StringComparison.Ordinal);
    }

    /// <summary>Si alguna de las frases está en la pregunta.</summary>
    public bool ContieneAlguna(IEnumerable<string> frases) => frases.Any(Contiene);

    /// <summary>Si alguna palabra de la pregunta empieza con el prefijo.</summary>
    public bool TienePalabraQueEmpiezaCon(string prefijo) =>
        Palabras.Any(palabra => palabra.StartsWith(prefijo, StringComparison.Ordinal));

    /// <summary>Si la pregunta nombra un año de cuatro cifras (1900–2099).</summary>
    public bool NombraUnAnio() =>
        Palabras.Any(palabra => palabra.Length == 4
            && int.TryParse(palabra, NumberStyles.None, CultureInfo.InvariantCulture, out var anio)
            && anio is >= 1900 and <= 2099);

    /// <summary>
    /// Cada aparición del número —en cifras o en palabras— con las palabras que la
    /// rodean, para buscar la señal de comparación que la acompaña.
    /// </summary>
    public IReadOnlyList<string> VentanasDelNumero(int numero, int antes = 4, int despues = 3)
    {
        var ventanas = new List<string>();

        for (var indice = 0; indice < Palabras.Count; indice++)
        {
            if (ValorDe(Palabras[indice]) != numero)
            {
                continue;
            }

            var desde = Math.Max(0, indice - antes);
            var hasta = Math.Min(Palabras.Count, indice + despues + 1);
            ventanas.Add($" {string.Join(' ', Palabras.Skip(desde).Take(hasta - desde))} ");
        }

        return ventanas;
    }

    private static int? ValorDe(string palabra)
    {
        if (int.TryParse(palabra, NumberStyles.None, CultureInfo.InvariantCulture, out var cifra))
        {
            return cifra;
        }

        return NumerosEnPalabras.TryGetValue(palabra, out var enPalabras) ? enPalabras : null;
    }
}
