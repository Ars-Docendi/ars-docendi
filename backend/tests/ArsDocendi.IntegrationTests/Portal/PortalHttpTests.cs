using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Modules.Portal.Contracts.Dtos;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Portal;

[Collection(ColeccionPostgres.Nombre)]
public sealed class PortalHttpTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "portal_http")
{
    private static readonly Guid Docente = Guid.Parse("a0000000-0000-4000-8000-000000000001");

    [Fact]
    public async Task Todos_los_endpoints_ejecutan_el_crud_completo()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        await VerificarSuperficieHttpAsync(cliente, ct);

        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, Docente.ToString());
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, "docente");

        Assert.NotNull(await cliente.GetFromJsonAsync<PerfilDocenteDto>("/api/portal/perfil", ct));

        var contacto = await PutAsync<ContactoDto>(cliente, "/api/portal/perfil/contacto",
            new GuardarContactoDto("11-5555-0000", "docente@example.com"), ct);
        Assert.Equal("docente@example.com", contacto.Mail);

        var cv = await PutAsync<CvDto>(cliente, "/api/portal/perfil/cv",
            new GuardarCvDto("cv.pdf", "synthetic://cv"), ct);
        Assert.Equal("cv.pdf", cv.Nombre);

        await EsperarAsync(cliente.PutAsJsonAsync("/api/portal/perfil/habilidades",
            new GuardarTagsDto(["C#", " c# "]), ct), HttpStatusCode.NoContent);
        await EsperarAsync(cliente.PutAsJsonAsync("/api/portal/perfil/intereses",
            new GuardarTagsDto(["C#", "Docencia"]), ct), HttpStatusCode.NoContent);

        var experiencia = await PostAsync<ExperienciaDto>(cliente, "/api/portal/perfil/experiencia",
            new GuardarExperienciaDto("Docente", "UNLaM", "Descripción", new(2020, 1, 1), null), ct);
        await EsperarAsync(cliente.PutAsJsonAsync($"/api/portal/perfil/experiencia/{experiencia.Id}",
            new GuardarExperienciaDto("Inválida", "UNLaM", "Descripción", new(2025, 1, 1), new(2020, 1, 1)), ct),
            HttpStatusCode.BadRequest);
        experiencia = await PutAsync<ExperienciaDto>(cliente,
            $"/api/portal/perfil/experiencia/{experiencia.Id}",
            new GuardarExperienciaDto("Profesora", "UNLaM", "Actualizada", new(2020, 1, 1), null), ct);
        Assert.Equal("Profesora", experiencia.Puesto);

        var educacion = await PostAsync<EducacionDto>(cliente, "/api/portal/perfil/educacion",
            new GuardarEducacionDto("Grado", "Ingeniería", "UNLaM", new(2010, 1, 1), new(2015, 1, 1)), ct);
        educacion = await PutAsync<EducacionDto>(cliente, $"/api/portal/perfil/educacion/{educacion.Id}",
            new GuardarEducacionDto("Maestría", "Software", "UNLaM", new(2016, 1, 1), new(2018, 1, 1)), ct);
        Assert.Equal("Maestría", educacion.Nivel);

        var certificacion = await PostAsync<CertificacionDto>(cliente, "/api/portal/perfil/certificaciones",
            new GuardarCertificacionDto("Cloud", "Proveedor", new(2025, 1, 1), null), ct);
        certificacion = await PutAsync<CertificacionDto>(cliente,
            $"/api/portal/perfil/certificaciones/{certificacion.Id}",
            new GuardarCertificacionDto("Cloud II", "Proveedor", new(2025, 1, 1), new(2026, 1, 1)), ct);
        Assert.Equal("Cloud II", certificacion.Nombre);

        var proyecto = await PostAsync<ProyectoDto>(cliente, "/api/portal/perfil/proyectos",
            new GuardarProyectoDto("Proyecto", "Directora", "Descripción", new(2024, 1, 1), null,
                "10.1000/test", "proyecto.pdf", "synthetic://proyecto"), ct);
        proyecto = await PutAsync<ProyectoDto>(cliente, $"/api/portal/perfil/proyectos/{proyecto.Id}",
            new GuardarProyectoDto("Proyecto II", "Investigadora", "Actualizado", new(2024, 1, 1), null,
                null, "proyecto-v2.pdf", "synthetic://proyecto-v2"), ct);
        Assert.Equal("proyecto-v2.pdf", proyecto.Documento?.Nombre);

        using (var otroDocente = host.CreateClient())
        {
            otroDocente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario,
                "a0000000-0000-4000-8000-000000000002");
            otroDocente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, "jefe_catedra");
            await EsperarAsync(otroDocente.DeleteAsync(
                $"/api/portal/perfil/experiencia/{experiencia.Id}", ct), HttpStatusCode.NotFound);
        }

        foreach (var ruta in new[]
        {
            $"experiencia/{experiencia.Id}",
            $"educacion/{educacion.Id}",
            $"certificaciones/{certificacion.Id}",
            $"proyectos/{proyecto.Id}",
        })
            await EsperarAsync(cliente.DeleteAsync($"/api/portal/perfil/{ruta}", ct), HttpStatusCode.NoContent);
        await EsperarAsync(cliente.DeleteAsync("/api/portal/perfil/cv", ct), HttpStatusCode.NoContent);

        var perfil = await cliente.GetFromJsonAsync<PerfilDocenteDto>("/api/portal/perfil", ct);
        Assert.NotNull(perfil);
        Assert.Null(perfil.Cv);
        Assert.DoesNotContain(perfil.Experiencia, x => x.Id == experiencia.Id);
        Assert.DoesNotContain(perfil.Educacion, x => x.Id == educacion.Id);
        Assert.DoesNotContain(perfil.Certificaciones, x => x.Id == certificacion.Id);
        Assert.DoesNotContain(perfil.Proyectos, x => x.Id == proyecto.Id);
        Assert.Contains(perfil.Habilidades, x => x.Termino == "C#");
        Assert.Contains(perfil.Intereses, x => x.Termino == "C#");
    }

    private WebApplicationFactory<Program> CrearHost() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:ArsDocendi", Cadena);
            builder.UseSetting($"{AutenticacionDesarrolloOptions.Seccion}:Enabled", bool.TrueString);
        });

    private static async Task VerificarSuperficieHttpAsync(HttpClient cliente, CancellationToken ct)
    {
        var swagger = await cliente.GetFromJsonAsync<JsonObject>("/swagger/v1/swagger.json", ct);
        var operaciones = swagger!["paths"]!.AsObject()
            .SelectMany(ruta => ruta.Value!.AsObject().Select(metodo => (ruta.Key, metodo.Key)))
            .ToArray();
        Assert.Equal(64, operaciones.Length);

        foreach (var (ruta, metodo) in operaciones)
        {
            var uri = Regex.Replace(ruta, "\\{[^}]+\\}", Guid.Empty.ToString());
            using var solicitud = new HttpRequestMessage(new HttpMethod(metodo.ToUpperInvariant()), uri);
            using var respuesta = await cliente.SendAsync(solicitud, ct);
            var publica = ruta.EndsWith("/ping", StringComparison.Ordinal)
                || ruta == "/api/desarrollo/identidades";
            Assert.Equal(publica ? HttpStatusCode.OK : HttpStatusCode.Unauthorized,
                respuesta.StatusCode);
        }
    }

    private async Task EjecutarSeedAsync(CancellationToken ct)
    {
        var sql = await File.ReadAllTextAsync(
            Path.Combine(BuscarRaizRepositorio(), "infra", "scripts", "seed-data", "sintetico.sql"), ct);
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            sql + "\nDELETE FROM portal.contactos " +
            "WHERE perfil_id = 'f0000000-0000-4000-8000-000000000002';",
            conexion)
        { CommandTimeout = 60 };
        await comando.ExecuteNonQueryAsync(ct);
    }

    private static async Task<T> PostAsync<T>(HttpClient cliente, string ruta, object datos, CancellationToken ct)
    {
        using var respuesta = await cliente.PostAsJsonAsync(ruta, datos, ct);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<T>(ct))!;
    }

    private static async Task<T> PutAsync<T>(HttpClient cliente, string ruta, object datos, CancellationToken ct)
    {
        using var respuesta = await cliente.PutAsJsonAsync(ruta, datos, ct);
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK,
            $"{respuesta.StatusCode}: {await respuesta.Content.ReadAsStringAsync(ct)}");
        return (await respuesta.Content.ReadFromJsonAsync<T>(ct))!;
    }

    private static async Task EsperarAsync(Task<HttpResponseMessage> solicitud, HttpStatusCode esperado)
    {
        using var respuesta = await solicitud;
        Assert.Equal(esperado, respuesta.StatusCode);
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
