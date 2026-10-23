using System.Net.Http.Json;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Modules.Asistente;
using Modules.Asistente.Api;
using Modules.Asistente.Application;
using Modules.Asistente.Contracts;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// <see cref="ConsultaDeMantenimiento"/> sobre <see cref="IDisponibilidadDelModulo"/>
/// (sistema-seccion-unificada, design.md D7, tarea 1.3).
/// </summary>
public sealed class ConsultaDeMantenimientoTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "consulta_de_mantenimiento")
{
    private static readonly Guid Sistemas = Guid.Parse("a0000000-0000-4000-8000-000000000007");

    [Fact]
    public async Task Refleja_el_toggle_activado_por_PATCH_mantenimiento()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, Sistemas.ToString());
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, "sys_admin");

        var disponibilidad = new DisponibilidadDelModuloReal(new CadenaDuena(Cadena));
        var consulta = new ConsultaDeMantenimiento(disponibilidad);

        Assert.False((await consulta.ConsultarAsync(TestContext.Current.CancellationToken)).Activo);

        await cliente.PatchAsJsonAsync(
            "/api/asistente/administracion/mantenimiento",
            new PedidoDeMantenimientoDto(true, "mantenimiento programado"),
            TestContext.Current.CancellationToken);

        Assert.True((await consulta.ConsultarAsync(TestContext.Current.CancellationToken)).Activo);

        await cliente.PatchAsJsonAsync(
            "/api/asistente/administracion/mantenimiento",
            new PedidoDeMantenimientoDto(false, null),
            TestContext.Current.CancellationToken);

        Assert.False((await consulta.ConsultarAsync(TestContext.Current.CancellationToken)).Activo);
    }

    [Fact]
    public void EstadoDeMantenimientoPublico_no_expone_razon_ni_actor()
    {
        // El público de sistema.estado.ver no tiene por qué enterarse de la
        // razón ni, indirectamente, de quién activó el modo mantenimiento
        // (design.md D7): esos datos siguen detrás de asistente.consultar.
        var propiedades = typeof(EstadoDeMantenimientoPublico).GetProperties().Select(p => p.Name);

        Assert.Equal(["Activo"], propiedades);
    }

    private WebApplicationFactory<Program> CrearHost() =>
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
        });
}
