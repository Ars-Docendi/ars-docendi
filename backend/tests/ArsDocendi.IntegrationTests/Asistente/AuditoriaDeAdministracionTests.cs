using System.Reflection;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Persistencia;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// El rastro append-only de administración (asistente-auditoria-de-administracion,
/// grupo 8 de asistente-administracion-de-uso).
/// </summary>
public sealed class AuditoriaDeAdministracionTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_auditoria_administracion")
{
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    private static readonly DateTimeOffset Ancla = new(2027, 3, 10, 12, 0, 0, TimeSpan.Zero);

    // -------------------------------------------------------------------- 8.1

    [Fact]
    public async Task RegistrarAsync_escribe_actor_accion_antes_y_despues()
    {
        var reloj = new RelojFijo(Ancla);
        var auditoria = new AuditoriaDeAdministracionReal(new CadenaDuena(Cadena), reloj);
        var ct = TestContext.Current.CancellationToken;

        await auditoria.RegistrarAsync(
            Secretaria, "presupuesto.rol", """{"cupo":0}""", """{"cupo":5}""", ct);

        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            "SELECT actor_id, ocurrido_en, accion, antes, despues FROM asistente.auditoria_administracion",
            conexion);
        await using var lector = await comando.ExecuteReaderAsync(ct);
        Assert.True(await lector.ReadAsync(ct));

        Assert.Equal(Secretaria, lector.GetGuid(0));
        Assert.Equal(Ancla, lector.GetFieldValue<DateTimeOffset>(1));
        Assert.Equal("presupuesto.rol", lector.GetString(2));
        Assert.Equal("""{"cupo":0}""", lector.GetString(3));
        Assert.Equal("""{"cupo":5}""", lector.GetString(4));
    }

    // -------------------------------------------------------------------- 8.2

    [Fact]
    public void El_puerto_de_auditoria_no_ofrece_actualizar_ni_borrar()
    {
        // "Append-only, no borrable" se garantiza no escribiendo ese código
        // (mismo criterio que auditoria_acceso_historial, design.md D10 de
        // asistente-historial-conversaciones): el puerto sólo tiene UN método.
        var metodos = typeof(IAuditoriaDeAdministracion).GetMethods();

        Assert.Single(metodos);
        Assert.Equal(nameof(IAuditoriaDeAdministracion.RegistrarAsync), metodos[0].Name);
    }

    [Fact]
    public void Ningun_controller_del_modulo_ofrece_actualizar_o_borrar_la_auditoria()
    {
        var acciones = typeof(Modules.Asistente.ModuleExtensions).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any());

        var culpables = acciones
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>()
                .Any(a => a.HttpMethods.Contains("PUT") || a.HttpMethods.Contains("DELETE"))
                && (m.GetCustomAttributes<RouteAttribute>().Any(r => r.Template?.Contains("auditoria") == true)
                    || m.Name.Contains("Auditoria", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.Empty(culpables);
    }

    // -------------------------------------------------------------------- 8.3

    [Fact]
    public async Task La_purga_respeta_la_ventana_propia_de_la_auditoria_de_administracion()
    {
        var reloj = new RelojFijo(Ancla);

        await SembrarFilaAsync(Ancla.AddDays(-400));
        await SembrarFilaAsync(Ancla.AddDays(-30));

        var purga = new PurgaDeRegistros(
            new CadenaDuena(Cadena),
            Options.Create(new OpcionesAsistente { RetencionDeAuditoriaDeAdministracionDias = 365 }),
            reloj,
            NullLogger<PurgaDeRegistros>.Instance);

        var borradas = await purga.PurgarAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, borradas);
        Assert.Equal(1L, await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.auditoria_administracion"));
    }

    // -------------------------------------------------------------------- 8.4
    // Cubierto por PrivilegiosLecturaTests.La_administracion_de_uso_es_inalcanzable...

    private async Task SembrarFilaAsync(DateTimeOffset ocurrido)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO asistente.auditoria_administracion (actor_id, ocurrido_en, accion, despues)
            VALUES (@actor, @ocurrido, 'presupuesto.rol', '{}')
            """, conexion);
        comando.Parameters.AddWithValue("actor", Secretaria);
        comando.Parameters.AddWithValue("ocurrido", ocurrido);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
