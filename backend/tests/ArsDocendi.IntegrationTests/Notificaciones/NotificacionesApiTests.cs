using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArsDocendi.IntegrationTests.Infraestructura;
using Modules.Notificaciones.Api;

namespace ArsDocendi.IntegrationTests.Notificaciones;

public sealed class NotificacionesApiTests(PostgresFixture postgres)
    : BaseNotificaciones(postgres, "notificaciones_api")
{
    private const string AdministrativoId = "a1000000-0000-4000-8000-000000000006";

    /// <summary>Gives Docente a second profile (administrativo) so the profile filter can be exercised.</summary>
    private async Task DarleSegundoPerfilAlDocenteAsync() =>
        await EjecutarAsync(
            $"INSERT INTO identity.user_roles(user_id, role_id) VALUES ('{Docente}', '{AdministrativoId}')");

    [Fact]
    public async Task Ping_responde_sin_autenticar()
    {
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        var respuesta = await cliente.GetAsync("/api/notificaciones/ping", Ct);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(Ct));
        Assert.Equal("notificaciones", cuerpo.RootElement.GetProperty("module").GetString());
        Assert.Equal("ok", cuerpo.RootElement.GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("GET", "/api/notificaciones/no-leidas/cantidad")]
    [InlineData("GET", "/api/notificaciones")]
    [InlineData("PATCH", "/api/notificaciones/aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa/leida")]
    [InlineData("PATCH", "/api/notificaciones/leidas")]
    public async Task Sin_sesion_todas_las_rutas_responden_401(string metodo, string ruta)
    {
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        using var peticion = new HttpRequestMessage(new HttpMethod(metodo), ruta);
        if (metodo == "PATCH") peticion.Content = JsonContent.Create(new { todas = true });
        using var respuesta = await cliente.SendAsync(peticion, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task La_cantidad_y_la_lista_siguen_al_usuario_y_al_perfil_activo()
    {
        await SembrarAsync();
        await DarleSegundoPerfilAlDocenteAsync();
        await InsertarAsync(Docente, "docente");
        await InsertarAsync(Docente, "docente");
        await InsertarAsync(Docente, "administrativo");
        await InsertarAsync(Docente, "administrativo", leida: true);
        await InsertarAsync(Docente, null); // any profile
        await InsertarAsync(Jefe, null); // someone else's
        using var host = CrearHost();

        using var comoDocente = ClienteDe(host, Docente, "docente");
        Assert.Equal(3, (await LeerAsync<CantidadNoLeidasDto>(
            await comoDocente.GetAsync("/api/notificaciones/no-leidas/cantidad", Ct))).Cantidad);
        var paginaDocente = await LeerAsync<PaginaNotificacionesDto>(await comoDocente.GetAsync("/api/notificaciones", Ct));
        Assert.Equal(3, paginaDocente.Total);
        Assert.DoesNotContain(paginaDocente.Datos, n => n.RolDestino == "administrativo");

        using var comoAdministrativo = ClienteDe(host, Docente, "administrativo");
        Assert.Equal(2, (await LeerAsync<CantidadNoLeidasDto>(
            await comoAdministrativo.GetAsync("/api/notificaciones/no-leidas/cantidad", Ct))).Cantidad);
        var leidas = await LeerAsync<PaginaNotificacionesDto>(
            await comoAdministrativo.GetAsync("/api/notificaciones?estado=LEIDAS", Ct));
        Assert.Equal("administrativo", Assert.Single(leidas.Datos).RolDestino);
    }

    [Fact]
    public async Task La_lista_pagina_de_a_20_ordenada_de_la_mas_nueva_a_la_mas_vieja()
    {
        await SembrarAsync();
        var inicio = DateTimeOffset.UtcNow.AddHours(-2);
        for (var i = 0; i < 45; i++) await InsertarAsync(Docente, null, inicio.AddMinutes(i), titulo: $"N{i}");
        await InsertarAsync(Docente, null, inicio, leida: true, titulo: "Leida");
        using var host = CrearHost();
        using var cliente = ClienteDe(host, Docente, "docente");

        var primera = await LeerAsync<PaginaNotificacionesDto>(
            await cliente.GetAsync("/api/notificaciones?estado=NO_LEIDAS", Ct));
        Assert.Equal(20, primera.Datos.Count);
        Assert.Equal((45, 1, 20, 3), (primera.Total, primera.Pagina, primera.Tamanio, primera.TotalPaginas));
        Assert.Equal("N44", primera.Datos[0].Titulo);
        Assert.Equal(primera.Datos.OrderByDescending(n => n.CreadaEn).Select(n => n.Id), primera.Datos.Select(n => n.Id));

        var tercera = await LeerAsync<PaginaNotificacionesDto>(
            await cliente.GetAsync("/api/notificaciones?estado=NO_LEIDAS&pagina=3", Ct));
        Assert.Equal(5, tercera.Datos.Count);
        Assert.Equal("N0", tercera.Datos[^1].Titulo);

        var todas = await LeerAsync<PaginaNotificacionesDto>(
            await cliente.GetAsync("/api/notificaciones?tamanio=100", Ct));
        Assert.Equal(46, todas.Total);
        Assert.Equal(1, todas.TotalPaginas);
    }

    [Fact]
    public async Task Con_el_mismo_instante_el_desempate_es_por_id_descendente()
    {
        await SembrarAsync();
        var instante = DateTimeOffset.UtcNow.AddMinutes(-5);
        for (var i = 0; i < 6; i++) await InsertarAsync(Docente, null, instante);
        using var host = CrearHost();
        using var cliente = ClienteDe(host, Docente, "docente");

        var pagina = await LeerAsync<PaginaNotificacionesDto>(await cliente.GetAsync("/api/notificaciones", Ct));
        Assert.Equal(pagina.Datos.OrderByDescending(n => n.Id).Select(n => n.Id), pagina.Datos.Select(n => n.Id));
    }

    [Theory]
    [InlineData("tamanio=101")]
    [InlineData("tamanio=0")]
    [InlineData("tamanio=abc")]
    [InlineData("pagina=0")]
    [InlineData("pagina=-1")]
    [InlineData("estado=TODAS")]
    public async Task Los_parametros_invalidos_de_la_lista_responden_400(string consulta)
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = ClienteDe(host, Docente, "docente");
        var respuesta = await cliente.GetAsync($"/api/notificaciones?{consulta}", Ct);
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Marcar_una_como_leida_es_idempotente_y_conserva_el_primer_instante()
    {
        await SembrarAsync();
        var id = await InsertarAsync(Docente, null);
        using var host = CrearHost();
        using var cliente = ClienteDe(host, Docente, "docente");

        var primera = await cliente.PatchAsync($"/api/notificaciones/{id}/leida", null, Ct);
        Assert.Equal(HttpStatusCode.OK, primera.StatusCode);
        var instante = (await LeerAsync<NotificacionDto>(primera)).LeidaEn;
        Assert.NotNull(instante);
        await Task.Delay(20, Ct);
        var segunda = await LeerAsync<NotificacionDto>(await cliente.PatchAsync($"/api/notificaciones/{id}/leida", null, Ct));
        Assert.Equal(instante, segunda.LeidaEn);
        Assert.Equal(0, (await LeerAsync<CantidadNoLeidasDto>(
            await cliente.GetAsync("/api/notificaciones/no-leidas/cantidad", Ct))).Cantidad);
    }

    [Fact]
    public async Task Marcar_la_de_otro_usuario_la_de_otro_perfil_o_una_inexistente_responde_404_y_no_la_toca()
    {
        await SembrarAsync();
        await DarleSegundoPerfilAlDocenteAsync();
        var ajena = await InsertarAsync(Jefe, null);
        var deOtroPerfil = await InsertarAsync(Docente, "administrativo");
        using var host = CrearHost();
        using var cliente = ClienteDe(host, Docente, "docente");

        foreach (var id in new[] { ajena, deOtroPerfil, Guid.NewGuid() })
        {
            var respuesta = await cliente.PatchAsync($"/api/notificaciones/{id}/leida", null, Ct);
            Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
            using var problema = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(Ct));
            Assert.Contains("notificacion-not-found", problema.RootElement.GetRawText());
        }

        Assert.All(await NotificacionesAsync(), n => Assert.Null(n.LeidaEn));
    }

    [Fact]
    public async Task Marcar_varias_por_ids_cuenta_solo_las_visibles_sin_leer_y_ignora_el_resto()
    {
        await SembrarAsync();
        await DarleSegundoPerfilAlDocenteAsync();
        var propias = new[] { await InsertarAsync(Docente, null), await InsertarAsync(Docente, null) };
        var yaLeida = await InsertarAsync(Docente, null, leida: true);
        var ajena = await InsertarAsync(Jefe, null);
        var deOtroPerfil = await InsertarAsync(Docente, "administrativo");
        using var host = CrearHost();
        using var cliente = ClienteDe(host, Docente, "docente");

        var respuesta = await cliente.PatchAsJsonAsync(
            "/api/notificaciones/leidas", new { ids = new[] { propias[0], propias[1], yaLeida, ajena, deOtroPerfil, Guid.NewGuid() } }, Ct);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(2, (await LeerAsync<CantidadActualizadaDto>(respuesta)).CantidadActualizada);

        var todas = await NotificacionesAsync();
        Assert.Null(todas.Single(n => n.Id == ajena).LeidaEn);
        Assert.Null(todas.Single(n => n.Id == deOtroPerfil).LeidaEn);
    }

    [Fact]
    public async Task Marcar_todas_respeta_el_perfil_y_el_corte_hasta()
    {
        await SembrarAsync();
        var corte = DateTimeOffset.UtcNow.AddHours(-1);
        var antes = await InsertarAsync(Docente, null, corte.AddMinutes(-10));
        var justo = await InsertarAsync(Docente, null, corte);
        var despues = await InsertarAsync(Docente, null, corte.AddMinutes(10));
        var ajena = await InsertarAsync(Jefe, null, corte.AddMinutes(-10));
        using var host = CrearHost();
        using var cliente = ClienteDe(host, Docente, "docente");

        var parcial = await cliente.PatchAsJsonAsync(
            "/api/notificaciones/leidas", new { todas = true, hasta = corte.ToString("O") }, Ct);
        Assert.Equal(2, (await LeerAsync<CantidadActualizadaDto>(parcial)).CantidadActualizada);
        var filas = await NotificacionesAsync();
        Assert.NotNull(filas.Single(n => n.Id == antes).LeidaEn);
        Assert.NotNull(filas.Single(n => n.Id == justo).LeidaEn);
        Assert.Null(filas.Single(n => n.Id == despues).LeidaEn);

        var resto = await cliente.PatchAsJsonAsync("/api/notificaciones/leidas", new { todas = true }, Ct);
        Assert.Equal(1, (await LeerAsync<CantidadActualizadaDto>(resto)).CantidadActualizada);
        Assert.Null((await NotificacionesAsync()).Single(n => n.Id == ajena).LeidaEn);
    }

    public static TheoryData<string, string, string> SeleccionesInvalidas() => new()
    {
        { "{}", "todas", "Enviá ids o todas: true" },
        { "{\"ids\":[\"aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa\"],\"todas\":true}", "todas", "Enviá ids o todas: true, pero no ambos a la vez" },
        { "{\"ids\":[\"aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa\"],\"hasta\":\"2026-10-15T12:00:00Z\"}", "hasta", "hasta solo se puede usar con todas: true" },
        { "{\"ids\":[]}", "ids", "ids debe tener al menos 1 elemento" },
        { "{\"ids\":\"x\"}", "ids", "ids debe ser una lista" },
        { "{\"ids\":[\"no-es-uuid\"]}", "ids", "Cada id debe ser un UUID" },
        { "{\"ids\":[\"aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa\",\"AAAAAAAA-AAAA-4AAA-8AAA-AAAAAAAAAAAA\"]}", "ids", "ids no puede tener elementos repetidos" },
        { "{\"todas\":false}", "todas", "todas solo acepta el valor true" },
        { "{\"todas\":true,\"hasta\":\"2026-10-15\"}", "hasta", "hasta debe ser una fecha ISO 8601 con hora y zona horaria (ej. 2026-10-15T12:00:00.000Z)" },
        { "{\"todas\":true,\"hasta\":\"2026-10-15T12:00:00\"}", "hasta", "hasta debe ser una fecha ISO 8601 con hora y zona horaria (ej. 2026-10-15T12:00:00.000Z)" },
    };

    [Theory]
    [MemberData(nameof(SeleccionesInvalidas))]
    public async Task Una_seleccion_invalida_responde_400_con_el_mensaje_del_campo(string cuerpo, string campo, string mensaje)
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = ClienteDe(host, Docente, "docente");
        using var contenido = new StringContent(cuerpo, System.Text.Encoding.UTF8, "application/json");
        var respuesta = await cliente.PatchAsync("/api/notificaciones/leidas", contenido, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        using var problema = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(Ct));
        Assert.Contains("notificaciones-seleccion-invalida", problema.RootElement.GetRawText());
        Assert.Equal(mensaje, problema.RootElement.GetProperty("errors").GetProperty(campo)[0].GetString());
    }

    [Fact]
    public async Task Mas_de_100_ids_responde_400()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var cliente = ClienteDe(host, Docente, "docente");
        var ids = Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToArray();
        var respuesta = await cliente.PatchAsJsonAsync("/api/notificaciones/leidas", new { ids }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        using var problema = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(Ct));
        Assert.Equal("ids no puede tener más de 100 elementos",
            problema.RootElement.GetProperty("errors").GetProperty("ids")[0].GetString());
    }
}
