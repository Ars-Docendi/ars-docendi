using System.Net;
using System.Text;
using System.Text.Json;
using ArsDocendi.Host.Auditoria;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Auditing;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Backend;

public sealed class PublicadorRemotoLotesTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "publicador_lotes")
{
    [Fact]
    public async Task Reintento_reusa_firma_y_no_republica_al_testigo_que_ya_confirmo()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var conexion = await AbrirConexionAsync();
        await using (var insertar = new NpgsqlCommand("""
            INSERT INTO identity.roles (id, code, name, scope, es_sistema, is_active)
            VALUES (@id, @code, 'Prueba publicación', 'global', FALSE, TRUE)
            """, conexion))
        {
            insertar.Parameters.AddWithValue("id", Guid.NewGuid());
            insertar.Parameters.AddWithValue("code", $"publicador-{Guid.NewGuid():N}");
            await insertar.ExecuteNonQueryAsync(ct);
        }

        await using var dataSource = NpgsqlDataSource.Create(Cadena);
        var preparador = new PreparadorLotesAuditoria(dataSource);
        var handler = new TestigoHttpHandler(fallarSegundoUnaVez: true);
        using var http = new HttpClient(handler);
        var publicador = new PublicadorRemotoLotes(preparador, http, Opciones());

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            publicador.PublicarSiguienteAsync("test", 100, ct));
        var resultado = await publicador.PublicarSiguienteAsync("test", 100, ct);

        Assert.True(resultado.DobleCustodiaRegistrada);
        Assert.Equal(1, handler.LlamadasFirmador);
        Assert.Equal(1, handler.LlamadasPrimario);
        Assert.Equal(2, handler.LlamadasSecundario);

        await using var consulta = new NpgsqlCommand("""
            SELECT signature IS NOT NULL AND signing_key_id IS NOT NULL
               AND primary_witnessed_at IS NOT NULL AND secondary_witnessed_at IS NOT NULL
              FROM audit.seal_batches
             WHERE environment = 'test'
            """, conexion);
        Assert.True((bool)(await consulta.ExecuteScalarAsync(ct))!);
    }

    [Fact]
    public async Task Reintento_despues_de_fallo_del_firmador_no_inventa_acuses()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var conexion = await AbrirConexionAsync();
        await InsertarRolAsync(conexion, ct, "firmador");

        await using var dataSource = NpgsqlDataSource.Create(Cadena);
        var handler = new TestigoHttpHandler(fallarFirmadorUnaVez: true);
        var publicador = new PublicadorRemotoLotes(
            new PreparadorLotesAuditoria(dataSource), new HttpClient(handler), Opciones());

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            publicador.PublicarSiguienteAsync("test", 100, ct));
        Assert.Equal(0, handler.LlamadasPrimario);
        Assert.Equal(0, handler.LlamadasSecundario);
        await using (var antes = new NpgsqlCommand("""
            SELECT signature IS NULL AND primary_witnessed_at IS NULL AND secondary_witnessed_at IS NULL
              FROM audit.seal_batches WHERE environment = 'test'
            """, conexion))
            Assert.True((bool)(await antes.ExecuteScalarAsync(ct))!);

        var resultado = await publicador.PublicarSiguienteAsync("test", 100, ct);
        Assert.True(resultado.DobleCustodiaRegistrada);
        Assert.Equal(2, handler.LlamadasFirmador);
        Assert.Equal(1, handler.LlamadasPrimario);
        Assert.Equal(1, handler.LlamadasSecundario);
    }

    [Fact]
    public async Task Lote_vacio_publica_heartbeat_firmado_sin_inventar_eventos()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var conexion = await AbrirConexionAsync();
        await InsertarRolAsync(conexion, ct, "vacio");

        await using var dataSource = NpgsqlDataSource.Create(Cadena);
        var handler = new TestigoHttpHandler();
        var publicador = new PublicadorRemotoLotes(
            new PreparadorLotesAuditoria(dataSource), new HttpClient(handler), Opciones());

        Assert.False((await publicador.PublicarSiguienteAsync("test", 100, ct)).SinTrabajo);
        var llamadasCompletas = (handler.LlamadasFirmador, handler.LlamadasPrimario, handler.LlamadasSecundario);
        var resultadoVacio = await publicador.PublicarSiguienteAsync("test", 100, ct);

        Assert.False(resultadoVacio.SinTrabajo);
        Assert.True(resultadoVacio.DobleCustodiaRegistrada);
        Assert.Equal((llamadasCompletas.LlamadasFirmador + 1, llamadasCompletas.LlamadasPrimario + 1, llamadasCompletas.LlamadasSecundario + 1),
            (handler.LlamadasFirmador, handler.LlamadasPrimario, handler.LlamadasSecundario));
        Assert.True((await new VerificadorLotesAuditoria(dataSource).VerificarAsync("test", ct)).HashesLocalesValidos);
    }

    [Fact]
    public async Task Acuse_con_digest_incorrecto_no_marca_testigo_como_confirmado()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var conexion = await AbrirConexionAsync();
        await using (var insertar = new NpgsqlCommand("""
            INSERT INTO identity.roles (id, code, name, scope, es_sistema, is_active)
            VALUES (@id, @code, 'Prueba acuse', 'global', FALSE, TRUE)
            """, conexion))
        {
            insertar.Parameters.AddWithValue("id", Guid.NewGuid());
            insertar.Parameters.AddWithValue("code", $"acuse-{Guid.NewGuid():N}");
            await insertar.ExecuteNonQueryAsync(ct);
        }

        await using var dataSource = NpgsqlDataSource.Create(Cadena);
        var publicador = new PublicadorRemotoLotes(
            new PreparadorLotesAuditoria(dataSource),
            new HttpClient(new TestigoHttpHandler(false, hashIncorrectoPrimario: true)),
            Opciones());

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            publicador.PublicarSiguienteAsync("test", 100, ct));
        await using var consulta = new NpgsqlCommand(
            "SELECT primary_witnessed_at IS NULL FROM audit.seal_batches WHERE environment = 'test'", conexion);
        Assert.True((bool)(await consulta.ExecuteScalarAsync(ct))!);
    }

    [Fact]
    public async Task Lote_completo_alterado_impide_publicar_un_sucesor()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var conexion = await AbrirConexionAsync();
        await InsertarRolAsync(conexion, ct, "cerrado");
        await using var dataSource = NpgsqlDataSource.Create(Cadena);
        var handler = new TestigoHttpHandler();
        using var http = new HttpClient(handler);
        var publicador = new PublicadorRemotoLotes(new PreparadorLotesAuditoria(dataSource), http, Opciones());
        Assert.True((await publicador.PublicarSiguienteAsync("test", 100, ct)).DobleCustodiaRegistrada);
        await using (var alterar = new NpgsqlCommand("UPDATE audit.change_log SET new_row = '{}'::jsonb WHERE seal_seq = 1", conexion))
            await alterar.ExecuteNonQueryAsync(ct);
        await InsertarRolAsync(conexion, ct, "sucesor");
        await Assert.ThrowsAsync<InvalidDataException>(() => publicador.PublicarSiguienteAsync("test", 100, ct));
        Assert.Equal(1, handler.LlamadasFirmador);
        await using var contar = new NpgsqlCommand("SELECT count(*) FROM audit.seal_batches", conexion);
        Assert.Equal(1L, await contar.ExecuteScalarAsync(ct));
    }

    private static async Task InsertarRolAsync(NpgsqlConnection conexion, CancellationToken ct, string prefijo)
    {
        await using var insertar = new NpgsqlCommand("""
            INSERT INTO identity.roles (id, code, name, scope, es_sistema, is_active)
            VALUES (@id, @code, 'Prueba publicación', 'global', FALSE, TRUE)
            """, conexion);
        insertar.Parameters.AddWithValue("id", Guid.NewGuid());
        insertar.Parameters.AddWithValue("code", $"{prefijo}-{Guid.NewGuid():N}");
        await insertar.ExecuteNonQueryAsync(ct);
    }

    private static OpcionesPublicacionLotes Opciones() => new(
        new Uri("https://signer.example.invalid/sign"), "token-signer",
        new Uri("https://witness-a.example.invalid/manifest"), "token-witness-a",
        new Uri("https://witness-b.example.invalid/hash"), "token-witness-b");

    private sealed class TestigoHttpHandler(
        bool fallarSegundoUnaVez = false,
        bool hashIncorrectoPrimario = false,
        bool fallarFirmadorUnaVez = false) : HttpMessageHandler
    {
        public int LlamadasFirmador { get; private set; }
        public int LlamadasPrimario { get; private set; }
        public int LlamadasSecundario { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/sign")
            {
                LlamadasFirmador++;
                if (fallarFirmadorUnaVez && LlamadasFirmador == 1)
                    return new(HttpStatusCode.ServiceUnavailable);
                return Json("""{"idClave":"clave-prueba","firmaBase64":"c2lnbmF0dXJl"}""");
            }

            using var documento = await JsonDocument.ParseAsync(
                await request.Content!.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            if (path == "/manifest")
            {
                LlamadasPrimario++;
                var hashManifiesto = documento.RootElement.GetProperty("hashManifiesto").GetString()!;
                return Json(JsonSerializer.Serialize(new
                {
                    hashAceptado = hashIncorrectoPrimario ? new string('0', 64) : hashManifiesto
                }));
            }

            if (path == "/hash")
            {
                LlamadasSecundario++;
                var hashManifiesto = documento.RootElement.GetProperty("hashManifiesto").GetString()!;
                if (fallarSegundoUnaVez && LlamadasSecundario == 1)
                    return new(HttpStatusCode.ServiceUnavailable);
                return Json(JsonSerializer.Serialize(new { hashAceptado = hashManifiesto }));
            }

            return new(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage Json(string contenido) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(contenido, Encoding.UTF8, "application/json")
        };
    }
}
