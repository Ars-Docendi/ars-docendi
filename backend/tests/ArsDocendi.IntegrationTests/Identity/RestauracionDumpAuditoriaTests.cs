using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ArsDocendi.Host.Auditoria;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Auditing;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Identity;

public sealed class RestauracionDumpAuditoriaTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "restauracion_dump")
{
    [Fact]
    public async Task Pg_dump_restaurado_en_base_descartable_no_puede_reanudar_sellos_atrasados()
    {
        var id = Postgres.ContenedorId;
        if (id is null) Assert.Skip("El ensayo pg_dump/pg_restore requiere Testcontainer aislado.");
        var ct = TestContext.Current.CancellationToken;
        await using var fuente = NpgsqlDataSource.Create(Cadena);
        await using var conexion = await AbrirConexionAsync();
        using var rsa = RSA.Create(2048);
        var preparador = new PreparadorLotesAuditoria(fuente);
        await InsertarRolAsync(conexion, "antes", ct);
        var primero = (await preparador.PrepararSiguienteAsync("test", 100, ct)).Lote!;
        await FirmarAsync(preparador, primero, rsa, ct);

        // Stream binario entre procesos: el daemon Docker no comparte necesariamente rutas con el runner.
        var copia = await DumpAsync(id!, new NpgsqlConnectionStringBuilder(Cadena).Database!, ct);
        await InsertarRolAsync(conexion, "despues", ct);
        var ultimo = (await preparador.PrepararSiguienteAsync("test", 100, ct)).Lote!;
        await FirmarAsync(preparador, ultimo, rsa, ct);
        var firma = rsa.SignHash(ultimo.HashManifiesto, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        var evidencia = new EvidenciaTestigoRemoto("test", ultimo.PrimeraSecuencia, ultimo.UltimaSecuencia,
            Convert.ToHexString(ultimo.HashManifiesto), "v1", Convert.ToBase64String(firma),
            Convert.ToBase64String(ultimo.Manifiesto), DateTimeOffset.UtcNow);
        using var http = new HttpClient(new LecturaFija(evidencia));
        var remoto = new VerificadorTestigosRemotos(http, new(
            new Uri("https://a.example.invalid/latest"), "read-a",
            new Uri("https://b.example.invalid/latest"), "read-b",
            new Dictionary<string, string> { ["v1"] = rsa.ExportSubjectPublicKeyInfoPem() }));

        var descartable = await Postgres.CrearBaseMigradaAsync("restaurado");
        await RestoreAsync(id!, new NpgsqlConnectionStringBuilder(descartable).Database!, copia, ct);
        await using var datosRestaurados = NpgsqlDataSource.Create(descartable);
        var verificador = new VerificadorAuditoriaAnclada(datosRestaurados, remoto);
        var estado = await verificador.VerificarAsync("test", ct);
        Assert.False(estado.Verificado);
        Assert.True(estado.RequiereReconciliacion);
        Assert.Equal(primero.UltimaSecuencia, estado.CursorLocal);
        Assert.Equal(ultimo.UltimaSecuencia, estado.CursorRemoto);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new CompuertaPublicacionAuditoria(datosRestaurados, remoto).ValidarAsync("test", ct));
        var publicador = new PublicadorRemotoLotes(new PreparadorLotesAuditoria(datosRestaurados), http,
            new OpcionesPublicacionLotes(new Uri("https://signer.example.invalid/sign"), "signer",
                new Uri("https://a.example.invalid/write"), "write-a",
                new Uri("https://b.example.invalid/write"), "write-b"),
            new CompuertaPublicacionAuditoria(datosRestaurados, remoto));
        await Assert.ThrowsAsync<InvalidDataException>(() => publicador.PublicarSiguienteAsync("test", 100, ct));
        await using var dbRestaurada = await datosRestaurados.OpenConnectionAsync(ct);
        await using var conteo = new NpgsqlCommand("SELECT count(*) FROM audit.seal_batches", dbRestaurada);
        Assert.Equal(1L, await conteo.ExecuteScalarAsync(ct));
    }

    private static async Task InsertarRolAsync(NpgsqlConnection db, string nombre, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO identity.roles(id,code,name,scope,es_sistema,is_active)
            VALUES(gen_random_uuid(), @nombre, @nombre, 'global', false, true)
            """, db);
        cmd.Parameters.AddWithValue("nombre", $"dump-{nombre}-{Guid.NewGuid():N}");
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task FirmarAsync(PreparadorLotesAuditoria preparador, LoteAuditoriaPreparado lote, RSA rsa, CancellationToken ct)
    {
        await preparador.RegistrarFirmaAsync(lote.Id, "v1",
            rsa.SignHash(lote.HashManifiesto, HashAlgorithmName.SHA256, RSASignaturePadding.Pss), ct);
        await preparador.RegistrarAcuseTestigoAsync(lote.Id, true, lote.HashManifiesto, ct);
        await preparador.RegistrarAcuseTestigoAsync(lote.Id, false, lote.HashManifiesto, ct);
    }

    private static ProcessStartInfo Comando(string id, params string[] args)
    {
        var info = new ProcessStartInfo("docker")
        {
            RedirectStandardInput = true, RedirectStandardOutput = true,
            RedirectStandardError = true, UseShellExecute = false
        };
        info.ArgumentList.Add("exec");
        if (args[0] == "pg_restore") info.ArgumentList.Add("-i");
        info.ArgumentList.Add(id);
        foreach (var arg in args) info.ArgumentList.Add(arg);
        return info;
    }

    private static async Task<byte[]> DumpAsync(string id, string baseDatos, CancellationToken ct)
    {
        using var proceso = Process.Start(Comando(id, "pg_dump", "-Fc", "-U", "postgres", "-d", baseDatos))!;
        await using var salida = new MemoryStream();
        var error = proceso.StandardError.ReadToEndAsync(ct);
        await proceso.StandardOutput.BaseStream.CopyToAsync(salida, ct);
        await proceso.WaitForExitAsync(ct);
        Assert.True(proceso.ExitCode == 0, await error);
        return salida.ToArray();
    }

    private static async Task RestoreAsync(string id, string baseDatos, byte[] copia, CancellationToken ct)
    {
        using var proceso = Process.Start(Comando(id, "pg_restore", "--clean", "--if-exists",
            "--no-owner", "--exit-on-error", "-U", "postgres", "-d", baseDatos))!;
        var error = proceso.StandardError.ReadToEndAsync(ct);
        var salida = proceso.StandardOutput.ReadToEndAsync(ct);
        await proceso.StandardInput.BaseStream.WriteAsync(copia, ct);
        proceso.StandardInput.Close();
        await proceso.WaitForExitAsync(ct);
        await salida;
        Assert.True(proceso.ExitCode == 0, await error);
    }

    private sealed class LecturaFija(EvidenciaTestigoRemoto evidencia) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = JsonContent.Create(request.RequestUri!.Host.StartsWith('b')
                ? evidencia with { ManifiestoBase64 = null } : evidencia) });
        }
    }
}
