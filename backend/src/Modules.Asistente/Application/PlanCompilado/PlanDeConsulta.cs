using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modules.Asistente.Application;

/// <summary>Una condición tal como la escribió el modelo, sin validar.</summary>
internal sealed record CondicionDelPlan(string Campo, string Operador, string Valor);

/// <summary>
/// El plan tal como lo escribió el modelo, sin validar (D3 de
/// <c>asistente-plan-compilado</c>).
/// </summary>
/// <param name="Filtros">Definen la población: el denominador de un porcentaje.</param>
/// <param name="Condiciones">Solo para un porcentaje: definen el numerador.</param>
internal sealed record PlanDeConsulta(
    bool Expresable,
    string Medida,
    IReadOnlyList<CondicionDelPlan> Filtros,
    IReadOnlyList<CondicionDelPlan> Condiciones)
{
    private static readonly JsonSerializerOptions Opciones = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Interpreta la salida del modelo, o devuelve nulo si no es un plan.
    /// </summary>
    /// <remarks>
    /// Toma del primer <c>{</c> al último <c>}</c>, igual que la generación de SQL:
    /// un servidor que no respeta la salida estructurada puede envolver el JSON
    /// en texto. Nulo no es un error del sistema: es una muestra inválida.
    /// </remarks>
    public static PlanDeConsulta? Interpretar(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var inicio = texto.IndexOf('{', StringComparison.Ordinal);
        var fin = texto.LastIndexOf('}');
        if (inicio < 0 || fin <= inicio)
        {
            return null;
        }

        try
        {
            var crudo = JsonSerializer.Deserialize<PlanCrudo>(texto[inicio..(fin + 1)], Opciones);
            if (crudo?.Medida is null)
            {
                return null;
            }

            return new PlanDeConsulta(
                crudo.Expresable,
                crudo.Medida.Trim(),
                Convertir(crudo.Filtros),
                Convertir(crudo.Condiciones));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static CondicionDelPlan[] Convertir(IReadOnlyList<CondicionCruda>? crudas) =>
        [.. (crudas ?? [])
            .Where(cruda => cruda is not null)
            .Select(cruda => new CondicionDelPlan(
                (cruda.Campo ?? string.Empty).Trim(),
                (cruda.Operador ?? string.Empty).Trim(),
                cruda.Valor switch
                {
                    JsonElement { ValueKind: JsonValueKind.String } texto => texto.GetString() ?? string.Empty,
                    JsonElement { ValueKind: JsonValueKind.Number } numero =>
                        numero.GetRawText(),
                    _ => string.Empty,
                }))];

    private sealed class PlanCrudo
    {
        [JsonPropertyName("expresable")]
        public bool Expresable { get; init; }

        [JsonPropertyName("medida")]
        public string? Medida { get; init; }

        [JsonPropertyName("filtros")]
        public IReadOnlyList<CondicionCruda>? Filtros { get; init; }

        [JsonPropertyName("condiciones")]
        public IReadOnlyList<CondicionCruda>? Condiciones { get; init; }
    }

    private sealed class CondicionCruda
    {
        [JsonPropertyName("campo")]
        public string? Campo { get; init; }

        [JsonPropertyName("operador")]
        public string? Operador { get; init; }

        // Un elemento y no un string: un modelo que no respeta el esquema puede
        // mandar el número sin comillas, y eso no tiene por qué invalidar la muestra.
        [JsonPropertyName("valor")]
        public JsonElement Valor { get; init; }
    }
}

/// <summary>Una condición validada contra el catálogo, con su valor ya tipado.</summary>
/// <param name="Indice">Su posición en el plan: identifica a la condición para ligar sus entidades.</param>
/// <param name="Codigo">El código del cargo, si el campo es un cargo.</param>
/// <param name="Numero">El entero, si el campo es numérico.</param>
/// <param name="Nombre">El nombre normalizado de la entidad, si el campo es una entidad.</param>
/// <param name="NombreOriginal">El nombre como lo escribió el modelo, para mostrarlo.</param>
internal sealed record CondicionValidada(
    int Indice,
    CampoDelPlan Campo,
    string Operador,
    string? Codigo,
    int? Numero,
    string? Nombre,
    string? NombreOriginal)
{
    /// <summary>La forma que se compara entre muestras.</summary>
    public string Canonica() => string.Create(CultureInfo.InvariantCulture,
        $"{Campo.Nombre}{Operador}{Codigo ?? Nombre ?? Numero?.ToString(CultureInfo.InvariantCulture)}");
}

/// <summary>Un plan que pasó el catálogo, el anclaje y el cierre.</summary>
internal sealed record PlanValidado(
    string Medida,
    IReadOnlyList<CondicionValidada> Filtros,
    IReadOnlyList<CondicionValidada> Condiciones)
{
    public IEnumerable<CondicionValidada> Todas => Filtros.Concat(Condiciones);

    /// <summary>
    /// La forma canónica: medida, filtros y condiciones ordenados. Dos muestras
    /// coinciden si y solo si su forma canónica es idéntica (D5).
    /// </summary>
    public string Canonico() =>
        $"{Medida}|f:{string.Join(';', Filtros.Select(c => c.Canonica()).Order(StringComparer.Ordinal))}"
        + $"|c:{string.Join(';', Condiciones.Select(c => c.Canonica()).Order(StringComparer.Ordinal))}";

    /// <summary>Si alguna condición lee el portal docente, que tiene su propio alcance.</summary>
    public bool TocaPortal => Todas.Any(c => c.Campo.Nombre == CatalogoDelPlan.AntiguedadDeclarada);
}
