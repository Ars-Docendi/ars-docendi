using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modules.Asistente.Application;

/// <summary>Un slot que una intención exige resolver.</summary>
/// <param name="Nombre">Cómo se lo nombra en el destino.</param>
/// <param name="Clase">Qué clase de valor del dominio admite.</param>
public sealed record SlotExigido(string Nombre, ClaseDeSlot Clase);

/// <summary>
/// Una intención del catálogo cerrado.
/// </summary>
/// <param name="Nombre">Identificador estable, usado también por los tests.</param>
/// <param name="Terminos">
/// Los términos que tienen que estar TODOS en la pregunta normalizada. Es un
/// conjunto y no una secuencia: el orden de las palabras no decide nada, porque
/// «¿en qué estado está el pedido de Pérez?» y «¿el pedido de Pérez en qué estado
/// está?» son la misma pregunta.
/// </param>
/// <param name="Slots">Lo que hay que resolver para poder enrutar.</param>
/// <param name="Destino">
/// A dónde enrutaría. Es una cadena lógica y nadie la invoca desde acá: es lo que
/// mantiene al módulo sin ninguna referencia nueva.
/// </param>
public sealed record Intencion(
    string Nombre,
    IReadOnlySet<string> Terminos,
    IReadOnlyList<SlotExigido> Slots,
    string Destino);

/// <summary>
/// El catálogo está mal escrito y no se puede cargar.
/// </summary>
/// <remarks>
/// El catálogo es un archivo, así que no lo protege el compilador. Sin este error
/// una intención mal escrita no rompería nada: simplemente no se reconocería nunca,
/// y la única señal sería que las preguntas siguen tomando el camino caro.
/// </remarks>
public sealed class CatalogoDeIntencionesInvalido(string mensaje)
    : InvalidOperationException(mensaje);

/// <summary>
/// El catálogo cerrado de intenciones del carril determinista.
/// </summary>
/// <remarks>
/// <b>Es un archivo declarativo y no código disperso.</b> Una intención mal
/// reconocida se diagnostica leyendo una tabla de cinco filas; repartida en
/// condicionales se diagnostica leyendo el módulo. Y es lo que hace barata la
/// disciplina de «una intención nueva, un caso de prueba»: el test itera el
/// catálogo, así que una intención sin prueba no entra.
///
/// Clase pura: no toca base ni red. Los valores contra los que se resuelven los
/// slots vienen de <see cref="ICatalogoDelDominio"/>.
/// </remarks>
public sealed class CatalogoDeIntenciones
{
    private static readonly JsonSerializerOptions Formato = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private CatalogoDeIntenciones(IReadOnlyList<Intencion> intenciones) =>
        Intenciones = intenciones;

    /// <summary>Las intenciones del catálogo, en el orden en que están declaradas.</summary>
    public IReadOnlyList<Intencion> Intenciones { get; }

    /// <summary>Ruta lógica del recurso embebido con el catálogo.</summary>
    public const string Recurso = "Modules.Asistente.Recursos.intenciones.json";

    /// <summary>Carga el catálogo desde su recurso embebido.</summary>
    public static CatalogoDeIntenciones Cargar()
    {
        var assembly = typeof(CatalogoDeIntenciones).Assembly;

        using var flujo = assembly.GetManifestResourceStream(Recurso)
            ?? throw new CatalogoDeIntencionesInvalido(
                $"No se encontró el recurso embebido '{Recurso}'. "
                + $"Recursos disponibles: {string.Join(", ", assembly.GetManifestResourceNames())}");

        using var lector = new StreamReader(flujo, System.Text.Encoding.UTF8);
        return Cargar(lector.ReadToEnd());
    }

    /// <summary>
    /// Carga el catálogo desde su JSON y lo valida.
    /// </summary>
    /// <exception cref="CatalogoDeIntencionesInvalido">
    /// Si falta un campo, una clase de slot no existe o un término no está
    /// normalizado.
    /// </exception>
    public static CatalogoDeIntenciones Cargar(string json)
    {
        Archivo? crudo;

        try
        {
            crudo = JsonSerializer.Deserialize<Archivo>(json, Formato);
        }
        catch (JsonException error)
        {
            // Una clase de slot inexistente llega hasta acá: el convertidor de enums
            // no la puede mapear. Se reescribe el mensaje porque el del serializador
            // habla de tipos de .NET y quien edita el catálogo está mirando un JSON.
            throw new CatalogoDeIntencionesInvalido(
                "El catálogo de intenciones no se pudo leer. Revisá que cada `clase` de "
                + $"slot sea una de: {string.Join(", ", Enum.GetNames<ClaseDeSlot>())}. "
                + $"Detalle: {error.Message}");
        }

        var declaradas = crudo?.Intenciones
            ?? throw new CatalogoDeIntencionesInvalido(
                "El catálogo de intenciones no declara ninguna intención.");

        return new CatalogoDeIntenciones([.. declaradas.Select(Validar)]);
    }

    private static Intencion Validar(IntencionCruda cruda)
    {
        var nombre = cruda.Nombre;

        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new CatalogoDeIntencionesInvalido(
                "Hay una intención sin nombre en el catálogo.");
        }

        if (cruda.Terminos is not { Count: > 0 })
        {
            throw new CatalogoDeIntencionesInvalido(
                $"La intención '{nombre}' no declara ningún término, así que reconocería "
                + "cualquier pregunta.");
        }

        if (cruda.Slots is not { Count: > 0 })
        {
            throw new CatalogoDeIntencionesInvalido(
                $"La intención '{nombre}' no exige ningún slot. Una intención sin slots "
                + "enrutaría sin saber sobre qué.");
        }

        if (string.IsNullOrWhiteSpace(cruda.Destino))
        {
            throw new CatalogoDeIntencionesInvalido(
                $"La intención '{nombre}' no declara destino.");
        }

        foreach (var termino in cruda.Terminos)
        {
            // El término del catálogo se compara contra la pregunta ya normalizada,
            // así que uno acentuado o en plural nunca coincidiría con nada. Falla al
            // cargar y no en silencio, que es la diferencia entre un error y un bug.
            var normalizado = NormalizadorLexico.Terminos(termino);

            if (normalizado.Count != 1 || !normalizado.Contains(termino))
            {
                throw new CatalogoDeIntencionesInvalido(
                    $"El término '{termino}' de la intención '{nombre}' no está normalizado. "
                    + $"Escribilo como: {string.Join(" ", normalizado)}");
            }
        }

        foreach (var slot in cruda.Slots.Where(s => string.IsNullOrWhiteSpace(s.Nombre)))
        {
            throw new CatalogoDeIntencionesInvalido(
                $"La intención '{nombre}' tiene un slot sin nombre.");
        }

        return new Intencion(
            nombre,
            cruda.Terminos.ToHashSet(StringComparer.Ordinal),
            [.. cruda.Slots.Select(s => new SlotExigido(s.Nombre, s.Clase))],
            cruda.Destino);
    }

    private sealed record Archivo(List<IntencionCruda>? Intenciones);

    private sealed record IntencionCruda(
        string? Nombre, List<string>? Terminos, List<SlotCrudo>? Slots, string? Destino);

    private sealed record SlotCrudo(string? Nombre, ClaseDeSlot Clase);
}
