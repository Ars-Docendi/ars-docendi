using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ArsDocendi.Host.Auditoria;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Auditing;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Identity;

public sealed class CompuertaPublicacionAuditoriaTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "compuerta_publicacion")
{
    [Fact]
    public async Task Firmador_con_firma_invalida_no_publica_en_ningun_testigo()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var datos = NpgsqlDataSource.Create(Cadena);
        using var rsa = RSA.Create(2048);
        var handler = new TestigosMutables();
        using var http = new HttpClient(handler);
        var remoto = new VerificadorTestigosRemotos(http, new(
            new Uri("https://a.example.invalid/latest"), "read-a",
            new Uri("https://b.example.invalid/latest"), "read-b",
            new Dictionary<string, string> { ["v1"] = rsa.ExportSubjectPublicKeyInfoPem() }));
        var publicador = new PublicadorRemotoLotes(new PreparadorLotesAuditoria(datos), http,
            new(new Uri("https://firmador.example.invalid/sign"), "sign",
                new Uri("https://a.example.invalid/manifest"), "write-a",
                new Uri("https://b.example.invalid/hash"), "write-b"),
            new CompuertaPublicacionAuditoria(datos, remoto));
        await Assert.ThrowsAsync<InvalidDataException>(() => publicador.PublicarSiguienteAsync("test", 100, ct));
        Assert.Equal(1, handler.FirmasSolicitadas);
        Assert.Equal(0, handler.Publicaciones);
    }

    [Fact]
    public async Task Genesis_y_reintento_parcial_permitidos_solo_con_evidencia_concordante()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var datos = NpgsqlDataSource.Create(Cadena);
        using var rsa = RSA.Create(2048);
        var handler = new TestigosMutables();
        using var http = new HttpClient(handler);
        var remoto = new VerificadorTestigosRemotos(http, new(
            new Uri("https://a.example.invalid/latest"), "read-a",
            new Uri("https://b.example.invalid/latest"), "read-b",
            new Dictionary<string, string> { ["v1"] = rsa.ExportSubjectPublicKeyInfoPem() }));
        var compuerta = new CompuertaPublicacionAuditoria(datos, remoto);
        await compuerta.ValidarAsync("test", ct); // Ambos custodios declaran 404: génesis.

        var preparador = new PreparadorLotesAuditoria(datos);
        var primero = (await preparador.PrepararSiguienteAsync("test", 100, ct)).Lote!;
        await compuerta.ValidarAsync("test", ct); // Pendiente aún no publicado.
        var evidencia1 = await FirmarAsync(preparador, primero, rsa, ct);
        handler.Primario = evidencia1;
        handler.Secundario = evidencia1 with { ManifiestoBase64 = null };
        await compuerta.ValidarAsync("test", ct);

        await using var conexion = await AbrirConexionAsync();
        await using (var cmd = new NpgsqlCommand("""
            INSERT INTO identity.roles(id,code,name,scope,es_sistema,is_active)
            VALUES(gen_random_uuid(),'compuerta-test','Rol','global',false,true)
            """, conexion)) await cmd.ExecuteNonQueryAsync(ct);
        var segundo = (await preparador.PrepararSiguienteAsync("test", 100, ct)).Lote!;
        var firma2 = rsa.SignHash(segundo.HashManifiesto, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        await preparador.RegistrarFirmaAsync(segundo.Id, "v1", firma2, ct);
        handler.Primario = new EvidenciaTestigoRemoto("test", segundo.PrimeraSecuencia,
            segundo.UltimaSecuencia, Convert.ToHexString(segundo.HashManifiesto), "v1",
            Convert.ToBase64String(firma2), Convert.ToBase64String(segundo.Manifiesto), DateTimeOffset.UtcNow);
        await compuerta.ValidarAsync("test", ct); // Primario adelantado, secundario anterior: reintento.
        handler.Secundario = null;
        await Assert.ThrowsAsync<InvalidDataException>(() => compuerta.ValidarAsync("test", ct));
        handler.Secundario = evidencia1 with { ManifiestoBase64 = null };
        handler.Primario = null;
        await Assert.ThrowsAsync<InvalidDataException>(() => compuerta.ValidarAsync("test", ct));
    }

    [Fact]
    public async Task Genesis_incompleta_no_se_interpreta_como_ausencia_de_sellos()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var datos = NpgsqlDataSource.Create(Cadena);
        using var rsa = RSA.Create(2048);
        var handler = new TestigosMutables { Secundario = new EvidenciaTestigoRemoto(
            "test", 1, 1, new string('a', 64), "", "", null, DateTimeOffset.UtcNow) };
        using var http = new HttpClient(handler);
        var remoto = new VerificadorTestigosRemotos(http, new(
            new Uri("https://a.example.invalid/latest"), "read-a",
            new Uri("https://b.example.invalid/latest"), "read-b",
            new Dictionary<string, string> { ["v1"] = rsa.ExportSubjectPublicKeyInfoPem() }));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new CompuertaPublicacionAuditoria(datos, remoto).ValidarAsync("test", ct));
    }

    private static async Task<EvidenciaTestigoRemoto> FirmarAsync(
        PreparadorLotesAuditoria preparador, LoteAuditoriaPreparado lote, RSA rsa, CancellationToken ct)
    {
        var firma = rsa.SignHash(lote.HashManifiesto, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        await preparador.RegistrarFirmaAsync(lote.Id, "v1", firma, ct);
        await preparador.RegistrarAcuseTestigoAsync(lote.Id, true, lote.HashManifiesto, ct);
        await preparador.RegistrarAcuseTestigoAsync(lote.Id, false, lote.HashManifiesto, ct);
        return new("test", lote.PrimeraSecuencia, lote.UltimaSecuencia,
            Convert.ToHexString(lote.HashManifiesto), "v1", Convert.ToBase64String(firma),
            Convert.ToBase64String(lote.Manifiesto), DateTimeOffset.UtcNow);
    }

    private sealed class TestigosMutables : HttpMessageHandler
    {
        public EvidenciaTestigoRemoto? Primario { get; set; }
        public EvidenciaTestigoRemoto? Secundario { get; set; }
        public int FirmasSolicitadas { get; private set; }
        public int Publicaciones { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Method == HttpMethod.Post)
            {
                if (request.RequestUri!.AbsolutePath == "/sign")
                {
                    FirmasSolicitadas++;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = JsonContent.Create(new { idClave = "v1", firmaBase64 = "c2lnbmF0dXJl" }) });
                }
                Publicaciones++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }
            Assert.Equal(HttpMethod.Get, request.Method);
            var evidencia = request.RequestUri!.Host.StartsWith('a') ? Primario : Secundario;
            return Task.FromResult(evidencia is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(evidencia) });
        }
    }
}
