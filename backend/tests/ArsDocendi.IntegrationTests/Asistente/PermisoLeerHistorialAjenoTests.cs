using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Persistencia;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// El permiso de soporte que habilita leer el historial ajeno del asistente.
/// </summary>
/// <remarks>
/// Nace vacío y es deliberado (design.md D10 de
/// asistente-historial-conversaciones): quién puede leer el historial de otro
/// usuario es una decisión del Departamento, tomada desde /membresia-roles y
/// no asumida al sembrar. Ni siquiera <c>sys_admin</c> lo recibe por default.
/// </remarks>
public sealed class PermisoLeerHistorialAjenoTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_permiso_historial_ajeno")
{
    private const string Recurso = "identity/019_identity_permiso_leer_historial_ajeno.sql";

    [Fact]
    public async Task Existe_exactamente_una_fila_del_permiso()
    {
        var filas = await EscalarAsync<long>(
            "SELECT count(*) FROM identity.permisos WHERE code = @code",
            ("code", Permisos.AsistenteLeerHistorialAjeno));

        Assert.Equal(1, filas);
    }

    [Fact]
    public async Task Ningun_rol_lo_tiene_ni_siquiera_sys_admin()
    {
        var conAcceso = await EscalarAsync<long>(
            """
            SELECT count(*)
              FROM identity.rol_permisos rp
              JOIN identity.permisos p ON p.id = rp.permiso_id
             WHERE p.code = @code
            """,
            ("code", Permisos.AsistenteLeerHistorialAjeno));

        Assert.Equal(0, conAcceso);
    }

    [Fact]
    public async Task Reaplicar_la_migracion_no_duplica_filas_ni_concede_nada()
    {
        var antesPermisos = await EscalarAsync<long>("SELECT count(*) FROM identity.permisos");

        await EjecutarMigracionAsync();
        await EjecutarMigracionAsync();

        Assert.Equal(antesPermisos, await EscalarAsync<long>("SELECT count(*) FROM identity.permisos"));
        Assert.Equal(0, await EscalarAsync<long>(
            """
            SELECT count(*)
              FROM identity.rol_permisos rp
              JOIN identity.permisos p ON p.id = rp.permiso_id
             WHERE p.code = @code
            """,
            ("code", Permisos.AsistenteLeerHistorialAjeno)));
    }

    [Fact]
    public void La_constante_esta_declarada_y_registrada()
    {
        Assert.Equal("asistente.leer_historial_ajeno", Permisos.AsistenteLeerHistorialAjeno);
        Assert.Contains(Permisos.AsistenteLeerHistorialAjeno, Permisos.Todos);
    }

    [Fact]
    public async Task La_politica_existe_en_el_Host_compuesto()
    {
        using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting($"ConnectionStrings:{CadenaDuena.Clave}", Cadena);
        });

        var proveedor = host.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var politica = await proveedor.GetPolicyAsync(Permisos.AsistenteLeerHistorialAjeno);

        Assert.NotNull(politica);
    }

    private Task EjecutarMigracionAsync() => EjecutarAsync(
        RecursosSql.Leer(typeof(IdentityDbContext).Assembly, Recurso));
}
