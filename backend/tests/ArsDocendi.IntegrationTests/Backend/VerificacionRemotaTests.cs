using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using ArsDocendi.Host.Auditoria;

namespace ArsDocendi.IntegrationTests.Backend;

public sealed class VerificacionRemotaTests
{
    [Fact]
    public async Task Testigos_solo_lectura_verifican_clave_fijada_y_detectan_backup_atrasado()
    {
        using var rsa = RSA.Create(2048);
        var evidencia = Crear(rsa);
        var handler = new LecturaHandler(evidencia);
        using var http = new HttpClient(handler);
        var verificador = new VerificadorTestigosRemotos(http, Opciones(rsa));
        var actual = await verificador.VerificarAsync("test", 10, TestContext.Current.CancellationToken);
        Assert.False(actual.RequiereReconciliacion);
        var atrasado = await verificador.VerificarAsync("test", 4, TestContext.Current.CancellationToken);
        Assert.True(atrasado.RequiereReconciliacion);
        Assert.All(handler.Metodos, metodo => Assert.Equal(HttpMethod.Get, metodo));
    }

    [Theory]
    [InlineData("firma")]
    [InlineData("clave")]
    [InlineData("ambiente")]
    [InlineData("testigo")]
    [InlineData("ausente")]
    public async Task Evidencia_invalida_o_ausente_no_da_verde(string alteracion)
    {
        using var rsa = RSA.Create(2048);
        var e = Crear(rsa);
        e = alteracion switch
        {
            "firma" => e with { FirmaBase64 = Convert.ToBase64String(new byte[256]) },
            "clave" => e with { IdClaveFirma = "no-confiable" },
            "ambiente" => e with { Ambiente = "prod" },
            _ => e
        };
        using var http = new HttpClient(new LecturaHandler(e, alteracion));
        var verificador = new VerificadorTestigosRemotos(http, Opciones(rsa));
        await Assert.ThrowsAnyAsync<Exception>(() => verificador.VerificarAsync("test", 10, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Sonda_no_confunde_frescura_con_contenido_y_falla_ante_silencio()
    {
        var ahora = new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);
        var reciente = new EstadoTestigosRemotos(10, new string('a', 64), ahora, ahora, false);
        Assert.False(SondaFrescuraAuditoria.Evaluar(reciente, ahora, ahora, TimeSpan.FromMinutes(5), false).Saludable);
        Assert.False(SondaFrescuraAuditoria.Evaluar(reciente, null, ahora, TimeSpan.FromMinutes(5), true).Saludable);
        var sano = SondaFrescuraAuditoria.Evaluar(reciente, ahora, ahora, TimeSpan.FromMinutes(5), true);
        Assert.True(sano.Saludable);
        Assert.False(sano.ContenidoIndependientementeVerificado);
        Assert.False(SondaFrescuraAuditoria.Evaluar(reciente, ahora.AddMinutes(-6), ahora, TimeSpan.FromMinutes(5), true).Saludable);
        Assert.False(SondaFrescuraAuditoria.Evaluar(reciente with { PrimarioEn = ahora.AddMinutes(-6) }, ahora, ahora, TimeSpan.FromMinutes(5), true).Saludable);
        Assert.False(SondaFrescuraAuditoria.Evaluar(reciente with { RequiereReconciliacion = true }, ahora, ahora, TimeSpan.FromMinutes(5), true).Saludable);
    }

    private static OpcionesLecturaTestigos Opciones(RSA rsa) => new(
        new Uri("https://a.example.invalid/latest"), "read-a",
        new Uri("https://b.example.invalid/latest"), "read-b",
        new Dictionary<string, string> { ["historica-v1"] = rsa.ExportSubjectPublicKeyInfoPem() });

    private static EvidenciaTestigoRemoto Crear(RSA rsa)
    {
        var manifiesto = JsonSerializer.SerializeToUtf8Bytes(new { version = 1, ambiente = "test", primeraSecuencia = 1, ultimaSecuencia = 10 });
        var hash = SHA256.HashData(manifiesto);
        return new("test", 1, 10, Convert.ToHexString(hash), "historica-v1",
            Convert.ToBase64String(rsa.SignHash(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)),
            Convert.ToBase64String(manifiesto), DateTimeOffset.UtcNow);
    }

    private sealed class LecturaHandler(EvidenciaTestigoRemoto evidencia, string? alteracion = null) : HttpMessageHandler
    {
        public List<HttpMethod> Metodos { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Metodos.Add(request.Method);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            if (alteracion == "ausente") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            var e = request.RequestUri!.Host.StartsWith('b')
                ? evidencia with { ManifiestoBase64 = null, HashManifiesto = alteracion == "testigo" ? new string('0', 64) : evidencia.HashManifiesto }
                : evidencia;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(e) });
        }
    }
}
