using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ArsDocendi.IntegrationTests.Tareas;

internal static class ClienteVersionado
{
    public static Task<HttpResponseMessage> PutVersionadoAsync(this HttpClient cliente, string ruta, object datos, CancellationToken ct) =>
        EnviarAsync(cliente, HttpMethod.Put, ruta, datos, ct);
    public static Task<HttpResponseMessage> PatchVersionadoAsync(this HttpClient cliente, string ruta, object datos, CancellationToken ct) =>
        EnviarAsync(cliente, HttpMethod.Patch, ruta, datos, ct);
    public static Task<HttpResponseMessage> PostVersionadoAsync(this HttpClient cliente, string ruta, object datos, CancellationToken ct) =>
        EnviarAsync(cliente, HttpMethod.Post, ruta, datos, ct);

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient cliente, HttpMethod metodo, string ruta, object datos, CancellationToken ct)
    {
        var cuerpo = JsonSerializer.SerializeToNode(datos, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
        if (metodo != HttpMethod.Post || ruta.EndsWith("/estado", StringComparison.Ordinal))
        {
            var entidad = ruta.EndsWith("/estado", StringComparison.Ordinal) || ruta.EndsWith("/avance", StringComparison.Ordinal)
                ? ruta[..ruta.LastIndexOf('/')]
                : ruta;
            using var lectura = await cliente.GetAsync(entidad, ct);
            if (lectura.IsSuccessStatusCode)
                cuerpo["version"] = (await lectura.Content.ReadFromJsonAsync<JsonObject>(ct))!["version"]!.DeepClone();
        }
        using var solicitud = new HttpRequestMessage(metodo, ruta) { Content = JsonContent.Create(cuerpo) };
        return await cliente.SendAsync(solicitud, ct);
    }
}
