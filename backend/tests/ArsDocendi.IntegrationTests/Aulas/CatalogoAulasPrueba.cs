using System.Net;
using System.Net.Http.Json;
using Modules.Aulas.Api;

namespace ArsDocendi.IntegrationTests.Aulas;

/// <summary>Aulas del catálogo por nombre, creadas por la API la primera vez que se piden.</summary>
internal static class CatalogoAulasPrueba
{
    /// <param name="gestor">Cliente autenticado con <c>aulas.gestionar</c>.</param>
    public static async Task<Guid> IdAsync(
        HttpClient gestor, string nombre, CancellationToken ct, int capacidad = 40)
    {
        var existentes = await gestor.GetFromJsonAsync<AulaDto[]>("/api/aulas/catalogo", ct);
        if (existentes!.FirstOrDefault(a => a.Nombre == nombre) is { } existente) return existente.Id;

        var respuesta = await gestor.PostAsJsonAsync(
            "/api/aulas/catalogo", new GuardarAulaDto(nombre, "aula", capacidad, null), ct);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<AulaDto>(ct))!.Id;
    }
}
