using System.Net.Http.Json;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Modules.Asistente;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>Host HTTP del asistente sobre la base aislada de cada suite.</summary>
public abstract class AsistenteHttpBase(PostgresFixture postgres, string prefijo, bool sembrada = false)
    : ClasePostgresAislada(postgres, prefijo, sembrada)
{
    protected WebApplicationFactory<Program> CrearHostAsistente(
        ProveedorGuionado? guionado = null,
        TimeProvider? reloj = null,
        Action<IWebHostBuilder>? configurar = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting($"ConnectionStrings:{CadenaDuena.Clave}", Cadena);
            builder.UseSetting($"{AutenticacionDesarrolloOptions.Seccion}:Enabled", "true");
            builder.UseSetting(
                $"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.RolSoloLectura)}",
                RolSoloLectura);
            builder.UseSetting(
                $"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.RolSoloLecturaPii)}",
                RolSoloLecturaPii);
            builder.UseSetting(
                $"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.PasswordSoloLectura)}",
                PostgresFixture.PasswordDeRol);
            builder.UseSetting(
                $"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.PasswordSoloLecturaPii)}",
                PostgresFixture.PasswordDeRol);

            if (guionado is not null)
            {
                builder.ConfigureTestServices(servicios =>
                    servicios.AddSingleton(new ProveedorBase(guionado)));
            }

            if (reloj is not null)
            {
                builder.ConfigureTestServices(servicios => servicios.AddSingleton(reloj));
            }

            configurar?.Invoke(builder);
        });

    protected static void Autenticar(HttpClient cliente, Guid usuario, string rol)
    {
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, usuario.ToString());
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, rol);
    }

    protected static async Task<T> LeerAsync<T>(HttpResponseMessage respuesta)
    {
        var ct = TestContext.Current.CancellationToken;
        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);
        Assert.True(respuesta.IsSuccessStatusCode, cuerpo);

        return (await respuesta.Content.ReadFromJsonAsync<T>(ct))!;
    }
}
