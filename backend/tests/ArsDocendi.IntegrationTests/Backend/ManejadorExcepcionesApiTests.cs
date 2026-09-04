using System.Text.Json;
using ArsDocendi.Host.Api;
using ArsDocendi.Shared.Aplicacion;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArsDocendi.IntegrationTests.Backend;

public sealed class ManejadorExcepcionesApiTests
{
    public static TheoryData<TipoErrorAplicacion, int> CasosEsperados => new()
    {
        { TipoErrorAplicacion.Validacion, 400 },
        { TipoErrorAplicacion.NoAutenticado, 401 },
        { TipoErrorAplicacion.Prohibido, 403 },
        { TipoErrorAplicacion.NoEncontrado, 404 },
        { TipoErrorAplicacion.Conflicto, 409 },
        { TipoErrorAplicacion.ReglaDeNegocio, 422 },
    };

    [Theory]
    [MemberData(nameof(CasosEsperados))]
    public async Task Mapea_categorias_esperadas_a_problem_details(
        TipoErrorAplicacion tipo,
        int statusEsperado)
    {
        var errores = tipo == TipoErrorAplicacion.Validacion
            ? new Dictionary<string, string[]> { ["upn"] = ["La UPN es obligatoria."] }
            : null;
        var respuesta = await EjecutarAsync(new ExcepcionAplicacion(
            tipo,
            "codigo-estable",
            "Mensaje seguro",
            errores));

        Assert.Equal(statusEsperado, respuesta.Status);
        Assert.Equal("application/problem+json", respuesta.ContentType);
        Assert.Equal("https://ars-docendi.unlam.edu.ar/errors/codigo-estable", respuesta.Json.GetProperty("type").GetString());
        Assert.Equal("/api/prueba", respuesta.Json.GetProperty("instance").GetString());
        Assert.Equal("trace-prueba", respuesta.Json.GetProperty("traceId").GetString());
        if (errores is not null)
        {
            Assert.Equal("La UPN es obligatoria.", respuesta.Json.GetProperty("errors").GetProperty("upn")[0].GetString());
        }
    }

    [Fact]
    public async Task Error_inesperado_no_expone_excepcion()
    {
        var respuesta = await EjecutarAsync(new InvalidOperationException("secreto interno"));

        Assert.Equal(500, respuesta.Status);
        Assert.Equal("Ocurrió un error inesperado", respuesta.Json.GetProperty("title").GetString());
        Assert.DoesNotContain("secreto interno", respuesta.Json.ToString(), StringComparison.Ordinal);
    }

    private static async Task<RespuestaProblema> EjecutarAsync(Exception excepcion)
    {
        var contexto = new DefaultHttpContext();
        contexto.TraceIdentifier = "trace-prueba";
        contexto.Request.Path = "/api/prueba";
        contexto.Response.Body = new MemoryStream();
        var manejador = new ManejadorExcepcionesApi(NullLogger<ManejadorExcepcionesApi>.Instance);

        Assert.True(await manejador.TryHandleAsync(contexto, excepcion, CancellationToken.None));
        contexto.Response.Body.Position = 0;
        var json = await JsonDocument.ParseAsync(contexto.Response.Body);
        return new RespuestaProblema(contexto.Response.StatusCode, contexto.Response.ContentType, json.RootElement.Clone());
    }

    private sealed record RespuestaProblema(int Status, string? ContentType, JsonElement Json);
}
