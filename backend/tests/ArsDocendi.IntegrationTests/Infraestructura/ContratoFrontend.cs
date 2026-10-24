using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ArsDocendi.IntegrationTests.Infraestructura;

/// <summary>
/// Respuestas reales de la API guardadas como fixtures que consumen los tests del frontend.
/// Compara la forma (claves y tipos JSON), no los valores, porque IDs y fechas cambian en cada corrida.
/// Con <c>ACTUALIZAR_CONTRATOS=1</c> reescribe el fixture con la respuesta actual.
/// </summary>
public static class ContratoFrontend
{
    public static void Verificar(string nombre, string json)
    {
        var ruta = Path.Combine(BuscarRaizRepositorio(), "frontend", "src", "test", "contratos", nombre);
        var real = JsonNode.Parse(json);
        if (Environment.GetEnvironmentVariable("ACTUALIZAR_CONTRATOS") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
            File.WriteAllText(ruta, real!.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }) + "\n");
            return;
        }

        Assert.True(File.Exists(ruta), $"Falta el contrato {nombre}: correr con ACTUALIZAR_CONTRATOS=1.");
        var esperada = Forma(JsonNode.Parse(File.ReadAllText(ruta)));
        var actual = Forma(real);
        Assert.True(esperada == actual,
            $"La respuesta cambió de forma respecto de frontend/src/test/contratos/{nombre}. " +
            "Si el cambio es intencional, regenerarlo con ACTUALIZAR_CONTRATOS=1 y adaptar el frontend.\n" +
            $"Esperada: {esperada}\nActual:   {actual}");
    }

    private static string Forma(JsonNode? nodo) => nodo switch
    {
        null => "null",
        JsonObject objeto => "{" + string.Join(",", objeto
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => $"{p.Key}:{Forma(p.Value)}")) + "}",
        JsonArray lista => lista.Count == 0
            ? "[]"
            : "[" + string.Join("|", lista.Select(Forma).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)) + "]",
        JsonValue valor => valor.GetValueKind() switch
        {
            JsonValueKind.True or JsonValueKind.False => "bool",
            JsonValueKind.Number => "number",
            _ => "string",
        },
        _ => "?",
    };

    private static string BuscarRaizRepositorio()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) return dir.FullName;
        throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
    }
}
