using System.Net;
using System.Net.Http.Json;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Modules.Designaciones.Api;
using Modules.Designaciones.Domain;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Designaciones;

[Collection(ColeccionPostgres.Nombre)]
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
        await EjecutarSeedAsync(ct);
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
        await EjecutarSeedAsync(ct);
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

    private static async Task<PedidoDto> PostPedido(HttpClient cliente, Guid persona, CancellationToken ct)
    {
        using var respuesta = await cliente.PostAsJsonAsync(
            "/api/designaciones/pedidos", Datos(persona), ct);
        Assert.True(respuesta.StatusCode == HttpStatusCode.Created,
            await respuesta.Content.ReadAsStringAsync(ct));
        return (await respuesta.Content.ReadFromJsonAsync<PedidoDto>(ct))!;
    }

    private static object Datos(Guid persona, uint? version = null, int horas = 10) => new
    {
        periodoId = Periodo,
        personaId = persona,
        materiaId = Materia,
        novedad = Novedades.SinNovedad,
        cargoSolicitadoId = (Guid?)null,
        dedicacionSolicitada = (string?)null,
        horas,
        horasInvestigacion = 0,
        horasExternas = 0,
        justificacion = (string?)null,
        tipoBaja = (string?)null,
        tipoBajaDetalle = (string?)null,
        adjuntos = Array.Empty<object>(),
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
            builder.ConfigureAppConfiguration((_, configuracion) =>
                configuracion.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ArsDocendi"] = Cadena,
                    ["DevelopmentAuthentication:Enabled"] = "true",
                }));
        });

    private async Task EjecutarSeedAsync(CancellationToken ct)
    {
        var sql = await File.ReadAllTextAsync(
            Path.Combine(BuscarRaizRepositorio(), "infra", "scripts", "seed-data", "sintetico.sql"), ct);
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion) { CommandTimeout = 60 };
        await comando.ExecuteNonQueryAsync(ct);
    }

    private static string BuscarRaizRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null)
        {
            if (File.Exists(Path.Combine(directorio.FullName, "CLAUDE.md"))) return directorio.FullName;
            directorio = directorio.Parent;
        }
        throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
    }
}
