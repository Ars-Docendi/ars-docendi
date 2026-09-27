using System.Net;
using System.Net.Http.Json;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Modules.Asistente;
using Modules.Asistente.Api;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// El modo mantenimiento por HTTP de verdad (asistente-modo-mantenimiento,
/// grupo 6 de asistente-administracion-de-uso).
/// </summary>
public sealed class AdministracionAsistenteControllerTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_administracion_controller")
{
    // sys_admin ya tiene asistente.administrar por default (migración 020).
    private static readonly Guid Sistemas = Guid.Parse("a0000000-0000-4000-8000-000000000007");
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");

    [Fact]
    public async Task Sin_el_permiso_el_toggle_se_rechaza_y_no_cambia_nada()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var respuesta = await cliente.PatchAsJsonAsync(
            "/api/asistente/administracion/mantenimiento",
            new PedidoDeMantenimientoDto(true, "mantenimiento programado"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.False(await EscalarAsync<bool>("SELECT activo FROM asistente.modo_mantenimiento"));
    }

    [Fact]
    public async Task Activar_sin_razon_se_rechaza_y_el_flag_no_cambia()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Sistemas, "sys_admin");

        var respuesta = await cliente.PatchAsJsonAsync(
            "/api/asistente/administracion/mantenimiento",
            new PedidoDeMantenimientoDto(true, "   "),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.False(await EscalarAsync<bool>("SELECT activo FROM asistente.modo_mantenimiento"));
        Assert.Equal(0L, await EscalarAsync<long>("SELECT count(*) FROM asistente.auditoria_administracion"));
    }

    [Fact]
    public async Task Con_el_permiso_y_razon_activa_y_audita_una_fila()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Sistemas, "sys_admin");

        var respuesta = await cliente.PatchAsJsonAsync(
            "/api/asistente/administracion/mantenimiento",
            new PedidoDeMantenimientoDto(true, "mantenimiento programado"),
            TestContext.Current.CancellationToken);

        var cuerpo = await LeerAsync<MantenimientoDto>(respuesta);
        Assert.True(cuerpo.Activo);
        Assert.Equal("mantenimiento programado", cuerpo.Razon);

        Assert.True(await EscalarAsync<bool>("SELECT activo FROM asistente.modo_mantenimiento"));
        Assert.Equal(1L, await EscalarAsync<long>("SELECT count(*) FROM asistente.auditoria_administracion"));

        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            "SELECT actor_id, accion FROM asistente.auditoria_administracion", conexion);
        await using var lector = await comando.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await lector.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(Sistemas, lector.GetGuid(0));
        Assert.Equal("mantenimiento.activar", lector.GetString(1));
    }

    [Fact]
    public async Task Desactivar_no_exige_razon_y_audita_otra_fila()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Sistemas, "sys_admin");

        await cliente.PatchAsJsonAsync(
            "/api/asistente/administracion/mantenimiento",
            new PedidoDeMantenimientoDto(true, "mantenimiento"),
            TestContext.Current.CancellationToken);

        var respuesta = await cliente.PatchAsJsonAsync(
            "/api/asistente/administracion/mantenimiento",
            new PedidoDeMantenimientoDto(false, null),
            TestContext.Current.CancellationToken);

        var cuerpo = await LeerAsync<MantenimientoDto>(respuesta);
        Assert.False(cuerpo.Activo);
        Assert.Null(cuerpo.Razon);

        Assert.Equal(2L, await EscalarAsync<long>("SELECT count(*) FROM asistente.auditoria_administracion"));
        Assert.Equal(1L, await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.auditoria_administracion WHERE accion = 'mantenimiento.desactivar'"));
    }

    // ------------------------------------------------------------ 9.1-9.3, uso

    [Fact]
    public async Task Sin_el_permiso_el_panel_de_uso_se_rechaza()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var respuesta = await cliente.GetAsync(
            "/api/asistente/administracion/uso", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Con_el_permiso_el_panel_de_uso_agrega_por_usuario_rol_y_organizacion()
    {
        await SembrarAsync();
        await SembrarTurnoAsync(Secretaria, "anthropic/claude-sonnet-5", "Respondida", 2, 100, 50, 10);

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Sistemas, "sys_admin");

        var uso = await LeerAsync<UsoDto>(await cliente.GetAsync(
            "/api/asistente/administracion/uso?periodo=mes", TestContext.Current.CancellationToken));

        var deSecretaria = Assert.Single(uso.PorUsuario, u => u.Clave == Secretaria.ToString());
        Assert.Equal(1, deSecretaria.Turnos);
        Assert.Equal(2, deSecretaria.LlamadasAlModelo);

        Assert.Contains(uso.PorRol, r => r.Clave == "secretaria" && r.Turnos == 1);
        Assert.Equal(1, uso.Organizacion.Turnos);
        Assert.True(uso.Organizacion.EsEstimado);
    }

    [Fact]
    public async Task El_panel_de_uso_nunca_trae_un_campo_del_registro_analitico()
    {
        // No hay ningún campo "pregunta" ni "categoria" en el DTO — el
        // agregado sólo puede venir de registro_operativo (design.md TD-012).
        var propiedades = typeof(UsoAgregadoDto).GetProperties().Select(p => p.Name);

        Assert.DoesNotContain("Pregunta", propiedades);
        Assert.DoesNotContain("Categoria", propiedades);
    }

    // ------------------------------------------------------------ 9.5, 9.6

    [Fact]
    public async Task Editar_el_cupo_de_un_rol_audita_antes_y_despues()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Sistemas, "sys_admin");

        var respuesta = await cliente.PutAsJsonAsync(
            "/api/asistente/administracion/presupuestos/roles/secretaria",
            new PedidoDeCupoDto(15),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.Equal(15, await EscalarAsync<int>(
            "SELECT cupo_diario_turnos FROM asistente.presupuesto_rol WHERE rol_code = 'secretaria'"));
        Assert.Equal(1L, await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.auditoria_administracion WHERE accion = 'presupuesto.rol'"));
    }

    [Fact]
    public async Task Sin_el_permiso_editar_el_cupo_de_un_rol_se_rechaza()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var respuesta = await cliente.PutAsJsonAsync(
            "/api/asistente/administracion/presupuestos/roles/secretaria",
            new PedidoDeCupoDto(15),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Editar_el_override_de_un_usuario_audita_antes_y_despues()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Sistemas, "sys_admin");

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/asistente/administracion/presupuestos/usuarios/{Secretaria}",
            new PedidoDeCupoDto(3),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.Equal(3, await EscalarAsync<int>(
            "SELECT cupo_diario_turnos FROM asistente.presupuesto_usuario WHERE actor_id = @actor AND vigente_hasta IS NULL",
            ("actor", Secretaria)));
        Assert.Equal(1L, await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.auditoria_administracion WHERE accion = 'presupuesto.usuario'"));
    }

    [Fact]
    public async Task Editar_el_tope_organizacional_audita_antes_y_despues()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Sistemas, "sys_admin");

        var respuesta = await cliente.PutAsJsonAsync(
            "/api/asistente/administracion/tope-organizacional",
            new PedidoDeTopeDto(500m),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.Equal(1L, await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.auditoria_administracion WHERE accion = 'tope_organizacional'"));
    }

    // ------------------------------------------------------------------ 12.8

    [Fact]
    public async Task Sin_el_permiso_leer_los_presupuestos_se_rechaza()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var respuesta = await cliente.GetAsync(
            "/api/asistente/administracion/presupuestos", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Sin_ningun_presupuesto_editado_el_estado_vacio_es_el_seed_de_la_006()
    {
        // El seed de 006_asistente_administracion.sql deja el tope en 0 y el
        // cupo de los siete roles de sistema en 0 (design.md, Open Questions):
        // un default adivinado y restrictivo es peor que arrancar "apagado".
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Sistemas, "sys_admin");

        var presupuestos = await LeerAsync<PresupuestosDto>(await cliente.GetAsync(
            "/api/asistente/administracion/presupuestos", TestContext.Current.CancellationToken));

        Assert.Equal(0m, presupuestos.TopeMensualUsd);
        Assert.Equal(0m, presupuestos.GastoEstimadoDelMes);
        Assert.True(presupuestos.EsEstimado);
        Assert.Empty(presupuestos.OverridesPorUsuario);
        Assert.Contains(presupuestos.CuposPorRol, c => c.Rol == "secretaria" && c.CupoDiarioTurnos == 0);
        Assert.Contains(presupuestos.CuposPorRol, c => c.Rol == "sys_admin" && c.CupoDiarioTurnos == 0);
    }

    [Fact]
    public async Task Los_valores_editados_por_PUT_se_leen_de_vuelta_por_GET()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Sistemas, "sys_admin");
        var ct = TestContext.Current.CancellationToken;

        await cliente.PutAsJsonAsync(
            "/api/asistente/administracion/presupuestos/roles/secretaria", new PedidoDeCupoDto(15), ct);
        await cliente.PutAsJsonAsync(
            $"/api/asistente/administracion/presupuestos/usuarios/{Secretaria}", new PedidoDeCupoDto(3), ct);
        await cliente.PutAsJsonAsync(
            "/api/asistente/administracion/tope-organizacional", new PedidoDeTopeDto(500m), ct);

        var presupuestos = await LeerAsync<PresupuestosDto>(
            await cliente.GetAsync("/api/asistente/administracion/presupuestos", ct));

        Assert.Equal(500m, presupuestos.TopeMensualUsd);
        Assert.Contains(presupuestos.CuposPorRol, c => c.Rol == "secretaria" && c.CupoDiarioTurnos == 15);

        var overrideDeSecretaria = Assert.Single(
            presupuestos.OverridesPorUsuario, o => o.ActorId == Secretaria);
        Assert.Equal(3, overrideDeSecretaria.CupoDiarioTurnos);
        Assert.Equal("Lucía Fernández", overrideDeSecretaria.NombreParaMostrar);
    }

    [Fact]
    public async Task El_gasto_del_mes_coincide_con_el_costo_que_reporta_el_panel_de_uso()
    {
        await SembrarAsync();
        await FijarPrecioVigenteAsync("anthropic", "claude-sonnet-5", 0.001m);
        await SembrarTurnoAsync(Secretaria, "anthropic/claude-sonnet-5", "Respondida", 1, 1000, 0, 10);

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Sistemas, "sys_admin");
        var ct = TestContext.Current.CancellationToken;

        var presupuestos = await LeerAsync<PresupuestosDto>(
            await cliente.GetAsync("/api/asistente/administracion/presupuestos", ct));

        var ahora = DateTimeOffset.UtcNow;
        var inicioDelMes = new DateTimeOffset(ahora.Year, ahora.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var usoDelMes = await LeerAsync<UsoDto>(await cliente.GetAsync(
            $"/api/asistente/administracion/uso?desde={Uri.EscapeDataString(inicioDelMes.ToString("O"))}"
            + $"&hasta={Uri.EscapeDataString(ahora.ToString("O"))}",
            ct));

        Assert.True(presupuestos.GastoEstimadoDelMes > 0m);
        Assert.Equal(usoDelMes.Organizacion.CostoEstimado, presupuestos.GastoEstimadoDelMes);
    }

    private async Task FijarPrecioVigenteAsync(string proveedor, string modelo, decimal precioPorTokenEntrada)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO asistente.tabla_de_precios
                (proveedor, modelo, precio_por_token_entrada, precio_por_token_salida,
                 precio_por_token_cache, version, vigente_desde)
            VALUES (@proveedor, @modelo, @precio, 0, 0, 1, now() - interval '1 day')
            """, conexion);
        comando.Parameters.AddWithValue("proveedor", proveedor);
        comando.Parameters.AddWithValue("modelo", modelo);
        comando.Parameters.AddWithValue("precio", precioPorTokenEntrada);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    // ------------------------------------------------------------------ apoyo

    private async Task SembrarTurnoAsync(
        Guid actor, string proveedor, string estado, int llamadas, int entrada, int salida, int latenciaMs)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO asistente.registro_operativo
                (actor_id, ocurrido_en, carril, estado, llamadas_al_modelo, tokens_de_entrada,
                 tokens_de_salida, latencia_ms, hubo_reintento, truncado, proveedor)
            VALUES (@actor, now(), 'Sql', @estado, @llamadas, @entrada, @salida, @latencia, false, false, @proveedor)
            """, conexion);
        comando.Parameters.AddWithValue("actor", actor);
        comando.Parameters.AddWithValue("estado", estado);
        comando.Parameters.AddWithValue("llamadas", llamadas);
        comando.Parameters.AddWithValue("entrada", entrada);
        comando.Parameters.AddWithValue("salida", salida);
        comando.Parameters.AddWithValue("latencia", latenciaMs);
        comando.Parameters.AddWithValue("proveedor", proveedor);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static void Autenticar(HttpClient cliente, Guid usuario, string rol)
    {
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, usuario.ToString());
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, rol);
    }

    private static async Task<T> LeerAsync<T>(HttpResponseMessage respuesta)
    {
        var ct = TestContext.Current.CancellationToken;
        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);

        Assert.True(respuesta.IsSuccessStatusCode, cuerpo);

        return (await respuesta.Content.ReadFromJsonAsync<T>(ct))!;
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
