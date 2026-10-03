using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// La lectura del <c>/metrics</c> del servidor local
/// (asistente-optimizaciones-modelo-local, design.md D8).
/// </summary>
public sealed class TelemetriaDelServidorLocalTests
{
    [Theory]
    [InlineData("http://arsdocendi-llm:8000/v1", "http://arsdocendi-llm:8000/metrics")]
    [InlineData("http://localhost:8080/v1/", "http://localhost:8080/metrics")]
    [InlineData("http://localhost:8080", "http://localhost:8080/metrics")]
    public void Las_metricas_estan_en_la_base_del_servidor(string url, string esperada) =>
        Assert.Equal(new Uri(esperada), TelemetriaDelServidorLocal.UrlDeMetricas(url));

    [Fact]
    public void Interpreta_las_metricas_de_vllm()
    {
        const string Texto = """
            # HELP vllm:num_requests_running Number of requests in model execution batches.
            # TYPE vllm:num_requests_running gauge
            vllm:num_requests_running{model_name="qwen3-8b"} 3.0
            vllm:num_requests_waiting{model_name="qwen3-8b"} 1.0
            vllm:kv_cache_usage_perc{model_name="qwen3-8b"} 0.42
            vllm:prefix_cache_queries_total{model_name="qwen3-8b"} 100000.0
            vllm:prefix_cache_hits_total{model_name="qwen3-8b"} 87000.0
            """;

        var estado = TelemetriaDelServidorLocal.Interpretar(Texto);

        Assert.Equal("vllm", estado.Motor);
        Assert.Equal(3, estado.EnCurso);
        Assert.Equal(1, estado.EnEspera);
        Assert.Equal(0.42, estado.UsoDeKvCache);
        Assert.Equal(0.87, estado.AciertosDeCacheDePrefijo!.Value, 3);
    }

    [Fact]
    public void Interpreta_las_metricas_de_llama_server_sin_inventar_las_que_no_publica()
    {
        const string Texto = """
            # TYPE llamacpp:requests_processing gauge
            llamacpp:requests_processing 2
            llamacpp:requests_deferred 0
            llamacpp:kv_cache_usage_ratio 0.31
            """;

        var estado = TelemetriaDelServidorLocal.Interpretar(Texto);

        Assert.Equal("llama.cpp", estado.Motor);
        Assert.Equal(2, estado.EnCurso);
        // Cero en espera es un dato, y está publicado.
        Assert.Equal(0, estado.EnEspera);
        // La caché de prefijo no la publica: queda nula, no en cero.
        Assert.Null(estado.AciertosDeCacheDePrefijo);
    }

    [Fact]
    public void Un_servidor_desconocido_no_inventa_numeros()
    {
        var estado = TelemetriaDelServidorLocal.Interpretar("otra_metrica 1\n");

        Assert.True(estado.Alcanzable);
        Assert.Null(estado.Motor);
        Assert.Null(estado.EnCurso);
        Assert.Null(estado.UsoDeKvCache);
    }
}
