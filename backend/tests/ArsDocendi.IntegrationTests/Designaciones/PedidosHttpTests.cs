using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Xml.Linq;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Modules.Designaciones.Api;
using Modules.Designaciones.Domain;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Designaciones;

public sealed class PedidosHttpTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "pedidos_http")
{
    private static readonly Guid Periodo = Guid.Parse("d4000000-0000-4000-8000-000000000001");
    private static readonly Guid Materia = Guid.Parse("70000000-0000-4000-8000-000000000101");
    private static readonly Guid Jefe = Guid.Parse("a0000000-0000-4000-8000-000000000002");
    private static readonly Guid Coordinador = Guid.Parse("a0000000-0000-4000-8000-000000000003");

    [Fact]
    public async Task Http_crea_obtiene_edita_envia_reenvia_y_elimina_con_historial()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Jefe, RolesCircuito.JefeCatedra);

        var creado = await PostPedido(cliente,
            Guid.Parse("d0000000-0000-4000-8000-000000000002"), ct);
        Assert.Matches(@"^\d{4}-\d{4}$", creado.Numero);
        Assert.Equal("crear", Assert.Single(creado.Historial).Accion);

        var editado = await cliente.PutAsJsonAsync($"/api/designaciones/pedidos/{creado.Id}",
            Datos(creado.Persona.Id, creado.Version, 18), ct);
        Assert.Equal(HttpStatusCode.OK, editado.StatusCode);
        var dtoEditado = (await editado.Content.ReadFromJsonAsync<PedidoDto>(ct))!;
        Assert.Equal(18, dtoEditado.Horas);

        using var enviar = new HttpRequestMessage(HttpMethod.Post,
            $"/api/designaciones/pedidos/{creado.Id}/enviar")
        {
            Content = JsonContent.Create(new AccionPedidoDto()),
        };
        enviar.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var respuestaEnviar = await cliente.SendAsync(enviar, ct);
        Assert.Equal(HttpStatusCode.OK, respuestaEnviar.StatusCode);
        var enviado = (await respuestaEnviar.Content.ReadFromJsonAsync<PedidoDto>(ct))!;
        Assert.Equal(EstadosPedido.EnRevisionCoordinador, enviado.Estado);
        Assert.Equal("enviar", enviado.Historial.Last().Accion);

        var obtenido = await cliente.GetFromJsonAsync<PedidoDto>(
            $"/api/designaciones/pedidos/{creado.Id}", ct);
        Assert.Equal(enviado.Estado, obtenido!.Estado);

        var devueltoId = Guid.Parse("d5000000-0000-4000-8000-000000000005");
        using var reenviar = new HttpRequestMessage(HttpMethod.Post,
            $"/api/designaciones/pedidos/{devueltoId}/reenviar")
        {
            Content = JsonContent.Create(new AccionPedidoDto()),
        };
        reenviar.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.OK, (await cliente.SendAsync(reenviar, ct)).StatusCode);

        var borrador = await PostPedido(cliente,
            Guid.Parse("d0000000-0000-4000-8000-000000000003"), ct);
        Assert.Equal(HttpStatusCode.NoContent,
            (await cliente.DeleteAsync($"/api/designaciones/pedidos/{borrador.Id}", ct)).StatusCode);
    }

    [Fact]
    public async Task Http_filtra_por_actor_e_ignora_ambito_falsificado_por_cliente()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Coordinador, RolesCircuito.CoordinadorCarrera);

        using var listado = await cliente.GetAsync(
            "/api/designaciones/pedidos?rol=secretaria&ambito=global", ct);
        var cuerpo = await listado.Content.ReadAsStringAsync(ct);
        Assert.True(listado.IsSuccessStatusCode, cuerpo);
        var pedidos = await listado.Content.ReadFromJsonAsync<PedidoDto[]>(ct);

        Assert.NotNull(pedidos);
        Assert.NotEmpty(pedidos);
        Assert.All(pedidos, p => Assert.Equal(
            Guid.Parse("c0000000-0000-4000-8000-000000000201"), p.Materia.CarreraId));

        cliente.DefaultRequestHeaders.Remove(AutenticacionDesarrolloHandler.HeaderRol);
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, RolesCircuito.Secretaria);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await cliente.GetAsync("/api/designaciones/pedidos", ct)).StatusCode);
    }

    [Theory]
    [InlineData("a0000000-0000-4000-8000-000000000003", RolesCircuito.CoordinadorCarrera, EstadosPedido.EnRevisionSecretaria)]
    [InlineData("a0000000-0000-4000-8000-000000000004", RolesCircuito.Secretaria, EstadosPedido.EnRevisionDecanato)]
    public async Task Propietario_edita_y_reenvia_un_pedido_devuelto_a_su_cargo(
        string usuarioId,
        string propietario,
        string etapaRetorno)
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        var pedidoId = Guid.Parse("d5000000-0000-4000-8000-000000000005");
        await using (var db = PostgresFixture.CrearDesignaciones(Cadena))
        {
            var pedido = await db.Pedidos.SingleAsync(p => p.Id == pedidoId, ct);
            pedido.PropietarioActual = propietario;
            pedido.EtapaRetorno = etapaRetorno;
            await db.SaveChangesAsync(ct);
        }
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Guid.Parse(usuarioId), propietario);
        var actual = await cliente.GetFromJsonAsync<PedidoDto>(
            $"/api/designaciones/pedidos/{pedidoId}", ct);

        using var edicion = await cliente.PutAsJsonAsync(
            $"/api/designaciones/pedidos/{pedidoId}",
            Datos(actual!.Persona.Id, actual.Version, 11), ct);
        Assert.Equal(HttpStatusCode.OK, edicion.StatusCode);
        using var reenvio = new HttpRequestMessage(
            HttpMethod.Post, $"/api/designaciones/pedidos/{pedidoId}/reenviar");
        reenvio.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var respuesta = await cliente.SendAsync(reenvio, ct);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(etapaRetorno,
            (await respuesta.Content.ReadFromJsonAsync<PedidoDto>(ct))!.Estado);
    }

    [Fact]
    public async Task Administrativo_puede_rechazar_desde_http()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(
            cliente,
            Guid.Parse("a0000000-0000-4000-8000-000000000006"),
            RolesCircuito.Administrativo);
        using var solicitud = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/designaciones/pedidos/d5000000-0000-4000-8000-000000000002/rechazar")
        {
            Content = JsonContent.Create(new AccionPedidoDto("No corresponde")),
        };
        solicitud.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        using var respuesta = await cliente.SendAsync(solicitud, ct);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(EstadosPedido.Rechazado,
            (await respuesta.Content.ReadFromJsonAsync<PedidoDto>(ct))!.Estado);
    }

    [Fact]
    public async Task Http_rechaza_crear_Sin_novedad_y_conserva_la_lectura_del_legado()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await using (var conexion = await AbrirConexionAsync())
        await using (var comando = new NpgsqlCommand("UPDATE designaciones.pedidos SET novedad = 'Sin novedad' WHERE id = 'd5000000-0000-4000-8000-000000000002'", conexion))
        {
            await comando.ExecuteNonQueryAsync(ct);
        }
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Jefe, RolesCircuito.JefeCatedra);

        using var respuesta = await cliente.PostAsJsonAsync(
            "/api/designaciones/pedidos",
            Datos(Guid.Parse("d0000000-0000-4000-8000-000000000003"), novedad: Novedades.SinNovedad),
            ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        var legado = await cliente.GetFromJsonAsync<PedidoDto>(
            "/api/designaciones/pedidos/d5000000-0000-4000-8000-000000000002", ct);
        Assert.Equal(Novedades.SinNovedad, legado!.Novedad);
        Assert.Equal("2026-9002", legado.Numero);
    }

    [Fact]
    public async Task Http_acepta_las_seis_dedicaciones_en_un_Cambio()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Jefe, RolesCircuito.JefeCatedra);
        var casos = new[]
        {
            (Guid.Parse("d0000000-0000-4000-8000-000000000002"),
                Guid.Parse("d6000000-0000-4000-8000-000000000001"),
                Guid.Parse("70000000-0000-4000-8000-000000000101")),
            (Guid.Parse("d0000000-0000-4000-8000-000000000003"),
                Guid.Parse("d6000000-0000-4000-8000-000000000002"),
                Guid.Parse("70000000-0000-4000-8000-000000000102")),
            (Guid.Parse("d0000000-0000-4000-8000-000000000015"),
                Guid.Parse("d6000000-0000-4000-8000-000000000006"),
                Guid.Parse("70000000-0000-4000-8000-000000000103")),
        };

        foreach (var (persona, dedicacionId, materiaId) in casos)
        {
            using var respuesta = await cliente.PostAsJsonAsync(
                "/api/designaciones/pedidos",
                Datos(persona, materiaId: materiaId, novedad: Novedades.CambioDeCargoODedicacion,
                    dedicacionSolicitadaId: dedicacionId),
                ct);

            Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
            var pedido = (await respuesta.Content.ReadFromJsonAsync<PedidoDto>(ct))!;
            Assert.Equal(dedicacionId, pedido.DedicacionSolicitadaId);
        }
    }

    [Fact]
    public async Task Recorrido_integrado_corrige_horas_reenvia_aprueba_y_exporta_sin_duplicar_continuidad()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        using var host = CrearHost();
        using var jefe = host.CreateClient();
        using var coordinador = host.CreateClient();
        using var secretaria = host.CreateClient();
        using var decanato = host.CreateClient();
        Autenticar(jefe, Jefe, RolesCircuito.JefeCatedra);
        Autenticar(coordinador, Coordinador, RolesCircuito.CoordinadorCarrera);
        Autenticar(secretaria, Guid.Parse("a0000000-0000-4000-8000-000000000004"), RolesCircuito.Secretaria);
        Autenticar(decanato, Guid.Parse("a0000000-0000-4000-8000-000000000005"), RolesCircuito.Decanato);

        var creado = await PostPedido(jefe, Guid.Parse("d0000000-0000-4000-8000-000000000004"), ct);
        var enviado = await Accionar(jefe, creado.Id, "enviar", null, ct);
        Assert.Equal(EstadosPedido.EnRevisionCoordinador, enviado.Estado);

        var devuelto = await Accionar(
            coordinador, creado.Id, "devolver", new AccionPedidoDto("Corregir las tres cargas"), ct);
        Assert.Equal(EstadosPedido.Devuelto, devuelto.Estado);

        var corregido = await jefe.PutAsJsonAsync(
            $"/api/designaciones/pedidos/{creado.Id}",
            Datos(creado.Persona.Id, devuelto.Version, 12, horasInvestigacion: 3, horasExternas: 2), ct);
        Assert.Equal(HttpStatusCode.OK, corregido.StatusCode);
        var pedidoCorregido = (await corregido.Content.ReadFromJsonAsync<PedidoDto>(ct))!;
        Assert.Equal(3, pedidoCorregido.HorasInvestigacion);
        Assert.Equal(2, pedidoCorregido.HorasExternas);

        var reenviado = await Accionar(jefe, creado.Id, "reenviar", null, ct);
        Assert.Equal(EstadosPedido.EnRevisionCoordinador, reenviado.Estado);
        await Accionar(coordinador, creado.Id, "aceptar", new AccionPedidoDto(), ct);
        await Accionar(secretaria, creado.Id, "aceptar", new AccionPedidoDto(), ct);
        var finalizado = await Accionar(decanato, creado.Id, "aceptar", new AccionPedidoDto(), ct);
        Assert.Equal(EstadosPedido.EnLote, finalizado.Estado);

        using var respuesta = await decanato.GetAsync($"/api/designaciones/periodos/{Periodo}/lote.xlsx", ct);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var zip = new ZipArchive(
            new MemoryStream(await respuesta.Content.ReadAsByteArrayAsync(ct)), ZipArchiveMode.Read);
        var propuesta = LeerXml(zip, "xl/worksheets/sheet1.xml").ToString();
        var altas = LeerXml(zip, "xl/worksheets/sheet2.xml").ToString();
        var bajas = LeerXml(zip, "xl/worksheets/sheet3.xml").ToString();
        Assert.Contains("Fernández, Lucía", propuesta, StringComparison.Ordinal);
        Assert.Contains("Fernández, Lucía", altas, StringComparison.Ordinal);
        Assert.Contains("Solicitud de alta", propuesta, StringComparison.Ordinal);
        Assert.DoesNotContain("Fernández, Lucía", bajas, StringComparison.Ordinal);
        Assert.DoesNotContain(finalizado.Id.ToString(), propuesta, StringComparison.Ordinal);
    }

    private static async Task<PedidoDto> PostPedido(HttpClient cliente, Guid persona, CancellationToken ct)
    {
        using var respuesta = await cliente.PostAsJsonAsync(
            "/api/designaciones/pedidos", Datos(persona), ct);
        Assert.True(respuesta.StatusCode == HttpStatusCode.Created,
            await respuesta.Content.ReadAsStringAsync(ct));
        return (await respuesta.Content.ReadFromJsonAsync<PedidoDto>(ct))!;
    }

    private static async Task<PedidoDto> Accionar(
        HttpClient cliente,
        Guid pedidoId,
        string accion,
        object? datos,
        CancellationToken ct)
    {
        using var solicitud = new HttpRequestMessage(
            HttpMethod.Post, $"/api/designaciones/pedidos/{pedidoId}/{accion}")
        {
            Content = datos is null ? null : JsonContent.Create(datos),
        };
        solicitud.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var respuesta = await cliente.SendAsync(solicitud, ct);
        Assert.True(respuesta.IsSuccessStatusCode, await respuesta.Content.ReadAsStringAsync(ct));
        return (await respuesta.Content.ReadFromJsonAsync<PedidoDto>(ct))!;
    }

    private static object Datos(
        Guid persona,
        uint? version = null,
        int horas = 10,
        int horasInvestigacion = 0,
        int horasExternas = 0,
        string novedad = Novedades.Alta,
        Guid? dedicacionSolicitadaId = null,
        Guid? materiaId = null) => new
    {
        periodoId = Periodo,
        personaId = persona,
        materiaId = materiaId ?? Materia,
        novedad,
        cargoSolicitadoId = Guid.Parse("c3000000-0000-4000-8000-000000000001"),
        dedicacionSolicitadaId = dedicacionSolicitadaId
            ?? Guid.Parse("d6000000-0000-4000-8000-000000000001"),
        horas,
        horasInvestigacion,
        horasExternas,
        justificacion = "Solicitud de alta",
        tipoBaja = (string?)null,
        tipoBajaDetalle = (string?)null,
        adjuntos = new[]
        {
            new { tipo = TiposAdjunto.Cv, nombre = "cv.pdf" },
            new { tipo = TiposAdjunto.DniFrente, nombre = "dni-frente.pdf" },
            new { tipo = TiposAdjunto.DniDorso, nombre = "dni-dorso.pdf" },
        },
        version,
    };

    private static void Autenticar(HttpClient cliente, Guid usuario, string rol)
    {
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, usuario.ToString());
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, rol);
    }

    private WebApplicationFactory<Program> CrearHost() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:ArsDocendi", Cadena);
            builder.UseSetting(
                $"{AutenticacionDesarrolloOptions.Seccion}:Enabled",
                "true");
        });

    private static XDocument LeerXml(ZipArchive zip, string nombre)
    {
        using var stream = zip.GetEntry(nombre)!.Open();
        return XDocument.Load(stream);
    }
}
