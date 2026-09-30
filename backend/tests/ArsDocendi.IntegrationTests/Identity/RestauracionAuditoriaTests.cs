using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ArsDocendi.Host.Auditoria;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Auditing;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Identity;

public sealed class RestauracionAuditoriaTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "restauracion_auditoria")
{
    [Fact]
    public async Task Rotacion_sin_transicion_firmada_no_certifica_la_cadena()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ds = NpgsqlDataSource.Create(Cadena);
        await using var c = await AbrirConexionAsync();
        using var anterior = RSA.Create(2048);
        using var nueva = RSA.Create(2048);
        var preparador = new PreparadorLotesAuditoria(ds);
        var primero = (await preparador.PrepararSiguienteAsync("test", 100, ct)).Lote!;
        await preparador.RegistrarFirmaAsync(primero.Id, "v1",
            anterior.SignHash(primero.HashManifiesto, HashAlgorithmName.SHA256, RSASignaturePadding.Pss), ct);
        await preparador.RegistrarAcuseTestigoAsync(primero.Id, true, primero.HashManifiesto, ct);
        await preparador.RegistrarAcuseTestigoAsync(primero.Id, false, primero.HashManifiesto, ct);
        await using (var cmd = new NpgsqlCommand("""
            INSERT INTO identity.roles(id,code,name,scope,es_sistema,is_active)
            VALUES(gen_random_uuid(), 'rotate-test', 'Rotación', 'global', false, true)
            """, c)) await cmd.ExecuteNonQueryAsync(ct);
        var segundo = (await preparador.PrepararSiguienteAsync("test", 100, ct)).Lote!;
        var firma = nueva.SignHash(segundo.HashManifiesto, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        await preparador.RegistrarFirmaAsync(segundo.Id, "v2", firma, ct);
        await preparador.RegistrarAcuseTestigoAsync(segundo.Id, true, segundo.HashManifiesto, ct);
        await preparador.RegistrarAcuseTestigoAsync(segundo.Id, false, segundo.HashManifiesto, ct);
        var evidencia = new EvidenciaTestigoRemoto("test", segundo.PrimeraSecuencia, segundo.UltimaSecuencia,
            Convert.ToHexString(segundo.HashManifiesto), "v2", Convert.ToBase64String(firma),
            Convert.ToBase64String(segundo.Manifiesto), DateTimeOffset.UtcNow);
        using var http = new HttpClient(new TestigosLectura(evidencia));
        var remoto = new VerificadorTestigosRemotos(http, new(new Uri("https://a.example.invalid/latest"), "read-a",
            new Uri("https://b.example.invalid/latest"), "read-b", new Dictionary<string, string>
            { ["v1"] = anterior.ExportSubjectPublicKeyInfoPem(), ["v2"] = nueva.ExportSubjectPublicKeyInfoPem() }));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new VerificadorAuditoriaAnclada(ds, remoto).VerificarAsync("test", ct));
        var pemNuevo = nueva.ExportSubjectPublicKeyInfoPem();
        var digestTransicion = VerificadorTestigosRemotos.CalcularDigestTransicion("test", "v1", "v2", pemNuevo);
        var transicion = new TransicionClaveFirma("test", "v1", "v2", Convert.ToBase64String(
            anterior.SignHash(digestTransicion, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)));
        var opciones = new OpcionesLecturaTestigos(new Uri("https://a.example.invalid/latest"), "read-a",
            new Uri("https://b.example.invalid/latest"), "read-b", new Dictionary<string, string>
            { ["v1"] = anterior.ExportSubjectPublicKeyInfoPem(), ["v2"] = pemNuevo }, [transicion]);
        Assert.True((await new VerificadorAuditoriaAnclada(ds,
            new VerificadorTestigosRemotos(http, opciones)).VerificarAsync("test", ct)).Verificado);
        var falsa = transicion with { Ambiente = "prod" };
        await Assert.ThrowsAsync<InvalidDataException>(() => new VerificadorAuditoriaAnclada(ds,
            new VerificadorTestigosRemotos(http, opciones with { Transiciones = [falsa] })).VerificarAsync("test", ct));
        var alterada = transicion with { FirmaAnteriorBase64 = Convert.ToBase64String(new byte[256]) };
        await Assert.ThrowsAsync<InvalidDataException>(() => new VerificadorAuditoriaAnclada(ds,
            new VerificadorTestigosRemotos(http, opciones with { Transiciones = [alterada] })).VerificarAsync("test", ct));
    }

    [Fact]
    public async Task Firma_historica_alterada_impide_verificar_ultimo_sello_valido()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ds = NpgsqlDataSource.Create(Cadena);
        await using var c = await AbrirConexionAsync();
        using var rsa = RSA.Create(2048);
        var preparador = new PreparadorLotesAuditoria(ds);
        var primero = (await preparador.PrepararSiguienteAsync("test", 100, ct)).Lote!;
        await preparador.RegistrarFirmaAsync(primero.Id, "v1", rsa.SignHash(primero.HashManifiesto, HashAlgorithmName.SHA256, RSASignaturePadding.Pss), ct);
        await preparador.RegistrarAcuseTestigoAsync(primero.Id, true, primero.HashManifiesto, ct);
        await preparador.RegistrarAcuseTestigoAsync(primero.Id, false, primero.HashManifiesto, ct);
        var ultimo = (await preparador.PrepararSiguienteAsync("test", 100, ct)).Lote!;
        var firma = rsa.SignHash(ultimo.HashManifiesto, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        await preparador.RegistrarFirmaAsync(ultimo.Id, "v1", firma, ct);
        var evidencia = new EvidenciaTestigoRemoto("test", ultimo.PrimeraSecuencia, ultimo.UltimaSecuencia,
            Convert.ToHexString(ultimo.HashManifiesto), "v1", Convert.ToBase64String(firma),
            Convert.ToBase64String(ultimo.Manifiesto), DateTimeOffset.UtcNow);
        using var http = new HttpClient(new TestigosLectura(evidencia));
        var remoto = new VerificadorTestigosRemotos(http, new(new Uri("https://a.example.invalid/latest"), "read-a",
            new Uri("https://b.example.invalid/latest"), "read-b", new Dictionary<string,string> { ["v1"] = rsa.ExportSubjectPublicKeyInfoPem() }));
        var verificador = new VerificadorAuditoriaAnclada(ds, remoto);
        Assert.True((await verificador.VerificarAsync("test", ct)).Verificado);
        await using (var cmd = new NpgsqlCommand("UPDATE audit.seal_batches SET signature=decode('00','hex') WHERE id=@id", c))
        {
            cmd.Parameters.AddWithValue("id", primero.Id);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await Assert.ThrowsAsync<InvalidDataException>(() => verificador.VerificarAsync("test", ct));
    }

    [Fact]
    public async Task Copia_atrasada_requiere_reconciliacion_sin_escribir_testigos()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ds = NpgsqlDataSource.Create(Cadena);
        await using var conexion = await AbrirConexionAsync();
        await using (var insertar = new NpgsqlCommand("""
            INSERT INTO identity.roles(id,code,name,scope,es_sistema,is_active)
            VALUES(gen_random_uuid(),'restore-test','Restauracion','global',false,true)
            """, conexion))
            await insertar.ExecuteNonQueryAsync(ct);
        var preparador = new PreparadorLotesAuditoria(ds);
        var lote = (await preparador.PrepararSiguienteAsync("test", 100, ct)).Lote!;
        using var rsa = RSA.Create(2048);
        var firma = rsa.SignHash(lote.HashManifiesto, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        await preparador.RegistrarFirmaAsync(lote.Id, "v1", firma, ct);
        await preparador.RegistrarAcuseTestigoAsync(lote.Id, true, lote.HashManifiesto, ct);
        await preparador.RegistrarAcuseTestigoAsync(lote.Id, false, lote.HashManifiesto, ct);
        var evidencia = new EvidenciaTestigoRemoto("test", lote.PrimeraSecuencia, lote.UltimaSecuencia,
            Convert.ToHexString(lote.HashManifiesto), "v1",
            Convert.ToBase64String(firma),
            Convert.ToBase64String(lote.Manifiesto), DateTimeOffset.UtcNow);
        using var http = new HttpClient(new TestigosLectura(evidencia));
        var remoto = new VerificadorTestigosRemotos(http, new(new Uri("https://a.example.invalid/latest"), "read-a",
            new Uri("https://b.example.invalid/latest"), "read-b", new Dictionary<string,string> { ["v1"] = rsa.ExportSubjectPublicKeyInfoPem() }));
        var verificador = new VerificadorAuditoriaAnclada(ds, remoto);
        Assert.True((await verificador.VerificarAsync("test", ct)).Verificado);

        // Recrea en esta base descartable el estado de un backup anterior al primer evento.
        // Los testigos conservan el checkpoint más nuevo y sólo admiten GET.
        await using (var retroceder = new NpgsqlCommand("""
            DELETE FROM audit.seal_batches;
            DELETE FROM audit.change_log WHERE seal_seq IS NOT NULL;
            UPDATE audit.seal_cursor SET last_seq = 0;
            """, conexion))
            await retroceder.ExecuteNonQueryAsync(ct);
        var resultado = await verificador.VerificarAsync("test", ct);
        Assert.False(resultado.Verificado);
        Assert.True(resultado.RequiereReconciliacion);
        Assert.Equal(0, resultado.CursorLocal);
        Assert.Equal(1, resultado.CursorRemoto);
        await using var consulta = new NpgsqlCommand("SELECT count(*) FROM audit.seal_batches", conexion);
        Assert.Equal(0L, await consulta.ExecuteScalarAsync(ct));
    }

    private sealed class TestigosLectura(EvidenciaTestigoRemoto evidencia) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = JsonContent.Create(request.RequestUri!.Host.StartsWith('b') ? evidencia with { ManifiestoBase64 = null } : evidencia) });
        }
    }
}
