using System.Net.Http.Json;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Modules.Asistente;
using Modules.Asistente.Api;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// <see cref="ConsultasDeAuditoriaDeAdministracion"/> contra filas escritas por
/// el controller real (sistema-seccion-unificada, design.md D1, tarea 1.2).
/// </summary>
public sealed class ConsultasDeAuditoriaDeAdministracionTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "consultas_auditoria_administracion")
{
    private static readonly Guid Sistemas = Guid.Parse("a0000000-0000-4000-8000-000000000007");
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");

    [Fact]
    public async Task Una_fila_por_tipo_de_accion_redondea_por_el_controller_real()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Sistemas, "sys_admin");

        await cliente.PutAsJsonAsync(
            "/api/asistente/administracion/presupuestos/roles/secretaria",
            new PedidoDeCupoDto(15), TestContext.Current.CancellationToken);
        await cliente.PutAsJsonAsync(
            $"/api/asistente/administracion/presupuestos/usuarios/{Secretaria}",
            new PedidoDeCupoDto(3), TestContext.Current.CancellationToken);
        await cliente.PutAsJsonAsync(
            "/api/asistente/administracion/tope-organizacional",
            new PedidoDeTopeDto(500m), TestContext.Current.CancellationToken);
        await cliente.PatchAsJsonAsync(
            "/api/asistente/administracion/mantenimiento",
            new PedidoDeMantenimientoDto(true, "mantenimiento programado"),
            TestContext.Current.CancellationToken);
        await cliente.PatchAsJsonAsync(
            "/api/asistente/administracion/mantenimiento",
            new PedidoDeMantenimientoDto(false, null),
            TestContext.Current.CancellationToken);

        var consultas = new ConsultasDeAuditoriaDeAdministracion(new CadenaDuena(Cadena));
        var lote = await consultas.ListarAsync(null, null, TestContext.Current.CancellationToken);

        Assert.False(lote.Truncado);
        Assert.Equal(5, lote.Eventos.Count);

        var porRol = Assert.Single(lote.Eventos, e => e.Tipo == "presupuesto.rol");
        Assert.Equal("secretaria", porRol.Clave);
        Assert.Null(porRol.UsuarioAfectado);
        var cupoRol = Assert.Single(porRol.Campos, c => c.Campo == "cupo");
        Assert.Equal("15", cupoRol.ValorNuevo);

        var porUsuario = Assert.Single(lote.Eventos, e => e.Tipo == "presupuesto.usuario");
        Assert.Null(porUsuario.Clave);
        Assert.Equal(Secretaria, porUsuario.UsuarioAfectado);
        var cupoUsuario = Assert.Single(porUsuario.Campos, c => c.Campo == "cupo");
        Assert.Equal("3", cupoUsuario.ValorNuevo);

        var tope = Assert.Single(lote.Eventos, e => e.Tipo == "tope_organizacional");
        var topeCampo = Assert.Single(tope.Campos, c => c.Campo == "tope_mensual_usd");
        Assert.Equal("500", topeCampo.ValorNuevo);

        var activar = Assert.Single(lote.Eventos, e => e.Tipo == "mantenimiento.activar");
        Assert.Equal("true", Assert.Single(activar.Campos, c => c.Campo == "activo").ValorNuevo);
        Assert.Equal(
            "mantenimiento programado",
            Assert.Single(activar.Campos, c => c.Campo == "razon").ValorNuevo);

        var desactivar = Assert.Single(lote.Eventos, e => e.Tipo == "mantenimiento.desactivar");
        Assert.Equal("false", Assert.Single(desactivar.Campos, c => c.Campo == "activo").ValorNuevo);

        Assert.All(lote.Eventos, e => Assert.Equal(Sistemas, e.ActorId));
    }

    [Fact]
    public async Task Presupuesto_de_usuario_con_cupo_previo_nulo_da_valor_anterior_nulo()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Sistemas, "sys_admin");

        await cliente.PutAsJsonAsync(
            $"/api/asistente/administracion/presupuestos/usuarios/{Secretaria}",
            new PedidoDeCupoDto(7), TestContext.Current.CancellationToken);

        var consultas = new ConsultasDeAuditoriaDeAdministracion(new CadenaDuena(Cadena));
        var lote = await consultas.ListarAsync(null, null, TestContext.Current.CancellationToken);

        var evento = Assert.Single(lote.Eventos);
        var cupo = Assert.Single(evento.Campos, c => c.Campo == "cupo");
        Assert.Null(cupo.ValorAnterior);
        Assert.Equal("7", cupo.ValorNuevo);
    }

    [Fact]
    public async Task El_cupo_de_2000_filas_marca_truncado_y_devuelve_exactamente_el_cupo()
    {
        await SembrarAsync();
        await InsertarFilasSinteticasAsync(2001);

        var consultas = new ConsultasDeAuditoriaDeAdministracion(new CadenaDuena(Cadena));
        var lote = await consultas.ListarAsync(null, null, TestContext.Current.CancellationToken);

        Assert.True(lote.Truncado);
        Assert.Equal(2000, lote.Eventos.Count);
    }

    [Fact]
    public async Task Sin_pasar_el_cupo_no_se_marca_truncado()
    {
        await SembrarAsync();
        await InsertarFilasSinteticasAsync(2000);

        var consultas = new ConsultasDeAuditoriaDeAdministracion(new CadenaDuena(Cadena));
        var lote = await consultas.ListarAsync(null, null, TestContext.Current.CancellationToken);

        Assert.False(lote.Truncado);
        Assert.Equal(2000, lote.Eventos.Count);
    }

    [Fact]
    public async Task El_rango_de_fechas_es_inclusivo_en_las_dos_puntas()
    {
        await SembrarAsync();
        var t1 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);
        var t3 = new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero);
        await InsertarFilaEnAsync(t1);
        await InsertarFilaEnAsync(t2);
        await InsertarFilaEnAsync(t3);

        var consultas = new ConsultasDeAuditoriaDeAdministracion(new CadenaDuena(Cadena));
        var lote = await consultas.ListarAsync(t1, t2, TestContext.Current.CancellationToken);

        Assert.Equal(2, lote.Eventos.Count);
        Assert.Contains(lote.Eventos, e => e.OcurridoEn == t1);
        Assert.Contains(lote.Eventos, e => e.OcurridoEn == t2);
        Assert.DoesNotContain(lote.Eventos, e => e.OcurridoEn == t3);
    }

    // ------------------------------------------------------------------------ apoyo

    private async Task InsertarFilasSinteticasAsync(int cantidad)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO asistente.auditoria_administracion (actor_id, ocurrido_en, accion, antes, despues)
            SELECT @actor, now() - (n || ' seconds')::interval, 'tope_organizacional',
                   '{"topeMensualUsd":0}', '{"topeMensualUsd":1}'
              FROM generate_series(1, @cantidad) AS n
            """, conexion);
        comando.Parameters.AddWithValue("actor", Sistemas);
        comando.Parameters.AddWithValue("cantidad", cantidad);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task InsertarFilaEnAsync(DateTimeOffset ocurridoEn)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO asistente.auditoria_administracion (actor_id, ocurrido_en, accion, antes, despues)
            VALUES (@actor, @ocurrido_en, 'tope_organizacional', '{"topeMensualUsd":0}', '{"topeMensualUsd":1}')
            """, conexion);
        comando.Parameters.AddWithValue("actor", Sistemas);
        comando.Parameters.AddWithValue("ocurrido_en", ocurridoEn);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static void Autenticar(HttpClient cliente, Guid usuario, string rol)
    {
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, usuario.ToString());
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, rol);
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
