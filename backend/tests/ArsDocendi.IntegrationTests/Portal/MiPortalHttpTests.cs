using System.Net;
using System.Net.Http.Json;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Modules.Designaciones.Api;
using Modules.Portal.Api;
using Modules.Portal.Contracts.Dtos;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Portal;

/// <summary>Vocabulario curado y designaciones vigentes de Mi Portal.</summary>
public sealed class MiPortalHttpTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "mi_portal_http")
{
    private static readonly Guid Docente = Guid.Parse("a0000000-0000-4000-8000-000000000001");
    private static readonly Guid JefeCatedra = Guid.Parse("a0000000-0000-4000-8000-000000000002");
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");

    // [BR-portal-003] [BR-portal-004]
    [Fact]
    public async Task Fusionar_mueve_los_usos_sin_duplicar_invalida_editores_y_resuelve_guardados_futuros()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost();
        using var docente = Cliente(host, Docente, "docente");
        var perfil = (await docente.GetFromJsonAsync<PerfilDocenteDto>("/api/portal/perfil", ct))!;
        // El seed ya le da "Bases de datos"; "BD" entra como sugerido.
        Assert.Equal(HttpStatusCode.NoContent, (await docente.PutAsJsonAsync("/api/portal/perfil/habilidades",
            new GuardarTagsDto(["Bases de datos", "BD"], perfil.VersionHabilidades), ct)).StatusCode);
        Assert.DoesNotContain("BD", (await docente.GetFromJsonAsync<string[]>("/api/portal/vocabulario", ct))!);

        using var secretaria = Cliente(host, Secretaria, "secretaria");
        var terminos = (await secretaria.GetFromJsonAsync<TerminoVocabularioDto[]>("/api/portal/vocabulario/terminos", ct))!;
        var bd = terminos.Single(t => t.Termino == "BD");
        var bases = terminos.Single(t => t.Termino == "Bases de datos");
        Assert.True(bd.Sugerido);

        Assert.Equal(HttpStatusCode.NoContent, (await secretaria.PostAsJsonAsync(
            $"/api/portal/vocabulario/terminos/{bd.Id}/fusionar", new FusionarTerminoDto(bases.Id), ct)).StatusCode);

        var despues = (await docente.GetFromJsonAsync<PerfilDocenteDto>("/api/portal/perfil", ct))!;
        Assert.Equal(["Bases de datos"], despues.Habilidades.Select(h => h.Termino));
        // Un editor abierto con la versión anterior a la fusión recibe 409.
        Assert.Equal(HttpStatusCode.Conflict, (await docente.PutAsJsonAsync("/api/portal/perfil/habilidades",
            new GuardarTagsDto(["BD"], perfil.VersionHabilidades + 1), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await docente.PutAsJsonAsync("/api/portal/perfil/intereses",
            new GuardarTagsDto(["bd"], despues.VersionIntereses), ct)).StatusCode);
        var intereses = (await docente.GetFromJsonAsync<PerfilDocenteDto>("/api/portal/perfil", ct))!.Intereses;
        Assert.Equal(["Bases de datos"], intereses.Select(h => h.Termino));
        Assert.DoesNotContain("BD", (await docente.GetFromJsonAsync<string[]>("/api/portal/vocabulario", ct))!);
    }

    // [BR-portal-002] [BR-portal-003]
    [Fact]
    public async Task Incorporar_ofrece_el_termino_y_renombrar_a_uno_existente_es_conflicto()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost();
        using var secretaria = Cliente(host, Secretaria, "secretaria");
        using var docente = Cliente(host, Docente, "docente");
        var terminos = (await secretaria.GetFromJsonAsync<TerminoVocabularioDto[]>("/api/portal/vocabulario/terminos", ct))!;
        var sugerido = terminos.Single(t => t.Termino == "Investigación educativa");
        Assert.DoesNotContain(sugerido.Termino, (await docente.GetFromJsonAsync<string[]>("/api/portal/vocabulario", ct))!);

        Assert.Equal(HttpStatusCode.NoContent, (await secretaria.PostAsync(
            $"/api/portal/vocabulario/terminos/{sugerido.Id}/incorporar", null, ct)).StatusCode);
        Assert.Contains(sugerido.Termino, (await docente.GetFromJsonAsync<string[]>("/api/portal/vocabulario", ct))!);

        var repetido = await secretaria.PutAsJsonAsync($"/api/portal/vocabulario/terminos/{sugerido.Id}",
            new RenombrarTerminoDto(" bases de datos "), ct);
        Assert.Equal(HttpStatusCode.Conflict, repetido.StatusCode);
        Assert.Contains("termino-repetido", await repetido.Content.ReadAsStringAsync(ct));
    }

    [Fact]
    public async Task La_curaduria_exige_su_permiso_aunque_se_administren_perfiles()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost();
        // jefe_catedra tiene portal.administrar pero no portal.vocabulario.administrar.
        using var jefe = Cliente(host, JefeCatedra, "jefe_catedra");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await jefe.GetAsync("/api/portal/vocabulario/terminos", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await jefe.PostAsync(
            $"/api/portal/vocabulario/terminos/{Guid.NewGuid()}/incorporar", null, ct)).StatusCode);
    }

    [Fact]
    public async Task Mis_designaciones_vigentes_son_solo_las_propias()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost();
        using var docente = Cliente(host, Docente, "docente");

        var propias = (await docente.GetFromJsonAsync<DesignacionPropiaDto[]>("/api/designaciones/vigentes/mias", ct))!;

        var unica = Assert.Single(propias);
        Assert.Equal(Guid.Parse("d6000000-0000-4000-8000-000000000001"), unica.Id);
        Assert.Equal(10, unica.Horas);
    }

    private static HttpClient Cliente(WebApplicationFactory<Program> host, Guid usuario, string rol)
    {
        var cliente = host.CreateClient();
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, usuario.ToString());
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, rol);
        return cliente;
    }

    private WebApplicationFactory<Program> CrearHost() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:ArsDocendi", Cadena);
            builder.UseSetting($"{AutenticacionDesarrolloOptions.Seccion}:Enabled", bool.TrueString);
        });

    private async Task EjecutarSeedAsync(CancellationToken ct)
    {
        var raiz = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(raiz!.FullName, "AGENTS.md"))) raiz = raiz.Parent;
        var sql = await File.ReadAllTextAsync(
            Path.Combine(raiz.FullName, "infra", "scripts", "seed-data", "sintetico.sql"), ct);
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion) { CommandTimeout = 60 };
        await comando.ExecuteNonQueryAsync(ct);
    }
}
